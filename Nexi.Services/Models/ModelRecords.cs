namespace Nexi.Services.Models;

public enum ModelSource
{
    Ollama,
    Gguf,
    Whisper
}

/// <summary>A model that is installed and ready to use, from Ollama, a local GGUF file, or Whisper.</summary>
public sealed record InstalledModelInfo(string Name, long SizeBytes, DateTimeOffset Modified, ModelSource Source)
{
    public string SizeText => FormatSize(SizeBytes);

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}

/// <summary>A model available for download from the HuggingFace catalog.</summary>
public sealed record CatalogModelInfo(
    string Id,
    string Name,
    string Description,
    string SizeText,
    string Quantization,
    string DownloadUrl,
    string RepoId);

/// <summary>Progress of an ongoing model download.</summary>
public sealed record ModelDownloadProgress(string ModelId, int Percent, string Status, long BytesReceived, long TotalBytes);
