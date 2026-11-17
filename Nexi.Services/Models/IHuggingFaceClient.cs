namespace Nexi.Services.Models;

/// <summary>Talks to the HuggingFace API: search GGUF models and download files with progress.</summary>
public interface IHuggingFaceClient
{
    Task<IReadOnlyList<CatalogModelInfo>> SearchModelsAsync(string query, CancellationToken ct = default);

    IAsyncEnumerable<ModelDownloadProgress> DownloadFileAsync(
        string url,
        string destinationPath,
        string modelId,
        CancellationToken ct = default);
}
