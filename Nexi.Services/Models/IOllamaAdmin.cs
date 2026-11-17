namespace Nexi.Services.Models;

/// <summary>Administrative operations against the local Ollama server (list, pull, delete, import).</summary>
public interface IOllamaAdmin
{
    Task<bool> IsServerReachableAsync(CancellationToken ct = default);

    Task<IReadOnlyList<InstalledModelInfo>> ListModelsAsync(CancellationToken ct = default);

    IAsyncEnumerable<ModelDownloadProgress> PullModelAsync(string name, CancellationToken ct = default);

    Task DeleteModelAsync(string name, CancellationToken ct = default);

    /// <summary>Registers a local GGUF file as an Ollama model via a generated Modelfile.</summary>
    IAsyncEnumerable<ModelDownloadProgress> ImportGgufAsync(string name, string ggufPath, CancellationToken ct = default);
}
