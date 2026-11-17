using Nexi.Services.Models;

namespace Nexi.Services.Tests;

public sealed class FakeOllamaAdmin : IOllamaAdmin
{
    public bool Reachable { get; set; } = true;

    public List<InstalledModelInfo> Models { get; } = new();

    public List<string> Pulled { get; } = new();

    public List<string> Deleted { get; } = new();

    public List<string> Imported { get; } = new();

    public TimeSpan PullDelay { get; set; } = TimeSpan.FromMilliseconds(10);

    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) => Task.FromResult(Reachable);

    public Task<IReadOnlyList<InstalledModelInfo>> ListModelsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InstalledModelInfo>>(Models);

    public async IAsyncEnumerable<ModelDownloadProgress> PullModelAsync(
        string name,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Pulled.Add(name);
        yield return new ModelDownloadProgress(name, 0, "pulling manifest", 0, 0);
        await Task.Delay(PullDelay, ct);
        yield return new ModelDownloadProgress(name, 50, "downloading", 500, 1000);
        await Task.Delay(PullDelay, ct);
        yield return new ModelDownloadProgress(name, 100, "success", 1000, 1000);
        Models.Add(new InstalledModelInfo(name, 1000, DateTimeOffset.UtcNow, ModelSource.Ollama));
    }

    public Task DeleteModelAsync(string name, CancellationToken ct = default)
    {
        Deleted.Add(name);
        Models.RemoveAll(m => m.Name == name);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ModelDownloadProgress> ImportGgufAsync(
        string name,
        string ggufPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Imported.Add($"{name}:{ggufPath}");
        yield return new ModelDownloadProgress(name, 0, "reading modelfile", 0, 0);
        yield return new ModelDownloadProgress(name, 100, "success", 0, 0);
        Models.Add(new InstalledModelInfo(name, 0, DateTimeOffset.UtcNow, ModelSource.Ollama));
        await Task.CompletedTask;
    }
}

public sealed class FakeHuggingFaceClient : IHuggingFaceClient
{
    public List<CatalogModelInfo> SearchResults { get; } = new();

    public List<(string Url, string Destination)> Downloads { get; } = new();

    public Task<IReadOnlyList<CatalogModelInfo>> SearchModelsAsync(string query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CatalogModelInfo>>(SearchResults);

    public async IAsyncEnumerable<ModelDownloadProgress> DownloadFileAsync(
        string url,
        string destinationPath,
        string modelId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Downloads.Add((url, destinationPath));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var bytes = new byte[1000];
        // Statement-form using: the stream must be closed before the File.Move below,
        // a declaration-form `await using var` in an iterator stays open until disposal.
        await using (var target = File.Create(destinationPath + ".part"))
        {
            for (var i = 0; i < 4; i++)
            {
                await target.WriteAsync(bytes.AsMemory(0, 250), ct);
                yield return new ModelDownloadProgress(modelId, (i + 1) * 25, "downloading", (i + 1) * 250, 1000);
            }
        }

        File.Move(destinationPath + ".part", destinationPath, overwrite: true);
        yield return new ModelDownloadProgress(modelId, 100, "done", 1000, 1000);
    }
}
