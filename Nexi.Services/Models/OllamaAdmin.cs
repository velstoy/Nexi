using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using Nexi.Data;

using OllamaSharp;
using OllamaSharp.Models;

namespace Nexi.Services.Models;

/// <summary><see cref="IOllamaAdmin"/> backed by OllamaSharp. The endpoint is read from settings on each call.</summary>
public sealed class OllamaAdmin : IOllamaAdmin
{
    private readonly INexiStore _store;
    private readonly ILogger<OllamaAdmin> _logger;

    public OllamaAdmin(INexiStore store, ILogger<OllamaAdmin> logger)
    {
        _store = store;
        _logger = logger;
    }

    // OllamaSharp's default HttpClient times out after 100 s, which kills long model pulls
    // and slow local generations - always pass a client with an explicit timeout.
    private async Task<(OllamaApiClient Client, HttpClient Http)> CreateClientAsync(CancellationToken ct, TimeSpan? timeout = null)
    {
        var settings = await _store.GetSettingsAsync(ct);
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "http://localhost:11434" : settings.BaseUrl;
        var http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = timeout ?? TimeSpan.FromHours(1)
        };
        return (new OllamaApiClient(http), http);
    }

    public async Task<bool> IsServerReachableAsync(CancellationToken ct = default)
    {
        try
        {
            var (client, http) = await CreateClientAsync(ct, TimeSpan.FromSeconds(10));
            using (http)
            using (client)
            {
                await client.ListLocalModelsAsync(ct);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama server not reachable");
            return false;
        }
    }

    public async Task<IReadOnlyList<InstalledModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        var (client, http) = await CreateClientAsync(ct);
        using (http)
        using (client)
        {
            var models = await client.ListLocalModelsAsync(ct);
            return models
                .Select(m => new InstalledModelInfo(m.Name, m.Size, m.ModifiedAt, ModelSource.Ollama))
                .OrderBy(m => m.Name)
                .ToList();
        }
    }

    public async IAsyncEnumerable<ModelDownloadProgress> PullModelAsync(
        string name,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var (client, http) = await CreateClientAsync(ct);
        using (http)
        using (client)
        {
            _logger.LogInformation("Pulling Ollama model {Model}", name);

            await foreach (var response in client.PullModelAsync(new PullModelRequest { Model = name }, ct))
            {
                if (response is null)
                {
                    continue;
                }

                yield return new ModelDownloadProgress(
                    name,
                    (int)Math.Clamp(response.Percent, 0, 100),
                    response.Status ?? "",
                    response.Completed,
                    response.Total);
            }
        }
    }

    public async Task DeleteModelAsync(string name, CancellationToken ct = default)
    {
        var (client, http) = await CreateClientAsync(ct);
        using (http)
        using (client)
        {
            _logger.LogInformation("Deleting Ollama model {Model}", name);
            await client.DeleteModelAsync(new DeleteModelRequest { Model = name }, ct);
        }
    }

    public async IAsyncEnumerable<ModelDownloadProgress> ImportGgufAsync(
        string name,
        string ggufPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = await _store.GetSettingsAsync(ct);
        var baseUrl = (string.IsNullOrWhiteSpace(settings.BaseUrl) ? "http://localhost:11434" : settings.BaseUrl).TrimEnd('/');
        _logger.LogInformation("Importing GGUF {Path} into Ollama as {Model}", ggufPath, name);

        // OllamaSharp 5 has no Modelfile field on CreateModelRequest, so post the create call directly.
        using var http = new HttpClient { Timeout = TimeSpan.FromHours(1) };
        var payload = new { model = name, modelfile = $"FROM {ggufPath}", stream = true };
        using var response = await http.PostAsJsonAsync($"{baseUrl}/api/create", payload, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            yield return new ModelDownloadProgress(name, 0, status, 0, 0);
        }
    }
}
