namespace Nexi.Services.Models;

/// <summary>
/// Manages installable AI models: Ollama models (pull/delete/import), GGUF files from the
/// HuggingFace catalog, and Whisper speech-to-text models.
/// </summary>
public interface IModelManager
{
    string ModelsDirectory { get; }

    /// <summary>Built-in GGUF catalog entries shown on the Models page.</summary>
    IReadOnlyList<CatalogModelInfo> PredefinedCatalog { get; }

    /// <summary>Whisper model options (id, file name, approximate size).</summary>
    IReadOnlyList<(string Id, string FileName, string SizeText)> WhisperModelOptions { get; }

    Task<bool> IsOllamaReachableAsync(CancellationToken ct = default);

    Task<IReadOnlyList<InstalledModelInfo>> ListOllamaModelsAsync(CancellationToken ct = default);

    IAsyncEnumerable<ModelDownloadProgress> PullOllamaModelAsync(string name, CancellationToken ct = default);

    Task DeleteOllamaModelAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogModelInfo>> SearchCatalogAsync(string query, CancellationToken ct = default);

    IAsyncEnumerable<ModelDownloadProgress> DownloadGgufAsync(CatalogModelInfo model, CancellationToken ct = default);

    IReadOnlyList<InstalledModelInfo> ListLocalGgufFiles();

    Task DeleteLocalGgufFileAsync(string fileName, CancellationToken ct = default);

    /// <summary>Registers a downloaded GGUF file as an Ollama model.</summary>
    IAsyncEnumerable<ModelDownloadProgress> ImportGgufToOllamaAsync(string fileName, string modelName, CancellationToken ct = default);

    IReadOnlyList<InstalledModelInfo> ListWhisperModels();

    IAsyncEnumerable<ModelDownloadProgress> DownloadWhisperModelAsync(string optionId, CancellationToken ct = default);

    Task DeleteWhisperModelAsync(string fileName, CancellationToken ct = default);

    /// <summary>Cancels an in-progress download for the given model id. Returns false if none is running.</summary>
    bool CancelDownload(string modelId);
}
