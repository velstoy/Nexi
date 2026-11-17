using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Nexi.Services.Models;

public sealed class HuggingFaceClient : IHuggingFaceClient
{
    private const string ApiBase = "https://huggingface.co/api";

    private readonly HttpClient _http;
    private readonly ILogger<HuggingFaceClient> _logger;

    public HuggingFaceClient(HttpClient http, ILogger<HuggingFaceClient> logger)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Nexi/1.0");
        _logger = logger;
    }

    public async Task<IReadOnlyList<CatalogModelInfo>> SearchModelsAsync(string query, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/models?search={Uri.EscapeDataString(query)}&limit=10&sort=downloads&direction=-1";
        using var doc = await GetJsonAsync(url, ct);
        var results = new List<CatalogModelInfo>();

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("id", out var idProp))
            {
                continue;
            }

            var repoId = idProp.GetString() ?? string.Empty;
            var catalog = await TryBuildCatalogEntryAsync(repoId, ct);
            if (catalog != null)
            {
                results.Add(catalog);
            }
        }

        return results;
    }

    private async Task<CatalogModelInfo?> TryBuildCatalogEntryAsync(string repoId, CancellationToken ct)
    {
        try
        {
            var treeUrl = $"{ApiBase}/models/{repoId}/tree/main?recursive=false";
            using var doc = await GetJsonAsync(treeUrl, ct);

            var ggufFiles = doc.RootElement.EnumerateArray()
                .Where(e => e.TryGetProperty("type", out var t) && t.GetString() == "file")
                .Select(e => new
                {
                    Path = e.GetProperty("path").GetString() ?? string.Empty,
                    Size = e.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0L
                })
                .Where(f => f.Path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (ggufFiles.Count == 0)
            {
                return null;
            }

            // Prefer common quantizations, then the largest file.
            var best = ggufFiles
                .OrderByDescending(f => f.Path.Contains("q4_k_m", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => f.Path.Contains("q8_0", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => f.Size)
                .First();

            var fileName = Path.GetFileName(best.Path);
            var quant = fileName.Contains("q4_k_m", StringComparison.OrdinalIgnoreCase) ? "Q4_K_M"
                : fileName.Contains("q8_0", StringComparison.OrdinalIgnoreCase) ? "Q8_0"
                : fileName.Contains("q5_k_m", StringComparison.OrdinalIgnoreCase) ? "Q5_K_M"
                : "GGUF";

            return new CatalogModelInfo(
                Id: repoId,
                Name: repoId.Split('/').Last(),
                Description: $"GGUF build from {repoId}",
                SizeText: InstalledModelInfo.FormatSize(best.Size),
                Quantization: quant,
                DownloadUrl: $"https://huggingface.co/{repoId}/resolve/main/{best.Path}",
                RepoId: repoId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve files for {RepoId}", repoId);
            return null;
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public async IAsyncEnumerable<ModelDownloadProgress> DownloadFileAsync(
        string url,
        string destinationPath,
        string modelId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var partPath = destinationPath + ".part";

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        long received = 0;
        var lastPercent = -1;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = File.Create(partPath))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;

                var percent = total > 0 ? (int)(received * 100 / total) : 0;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    yield return new ModelDownloadProgress(modelId, percent, "downloading", received, total);
                }
            }
        }

        File.Move(partPath, destinationPath, overwrite: true);
        yield return new ModelDownloadProgress(modelId, 100, "done", received, total);
    }
}
