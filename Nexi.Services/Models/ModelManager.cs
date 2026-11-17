using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using Nexi.Data;

using Whisper.net.Ggml;

namespace Nexi.Services.Models;

public sealed class ModelManager : IModelManager
{
    private readonly IOllamaAdmin _ollama;
    private readonly IHuggingFaceClient _huggingFace;
    private readonly INexiStore _store;
    private readonly ILogger<ModelManager> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _downloads = new();
    private readonly Func<GgmlType, CancellationToken, Task<Stream>>? _whisperModelStream;

    public ModelManager(
        IOllamaAdmin ollama,
        IHuggingFaceClient huggingFace,
        INexiStore store,
        ILogger<ModelManager> logger,
        string? modelsDirectory = null,
        Func<GgmlType, CancellationToken, Task<Stream>>? whisperModelStream = null)
    {
        _ollama = ollama;
        _huggingFace = huggingFace;
        _store = store;
        _logger = logger;
        _whisperModelStream = whisperModelStream;
        ModelsDirectory = modelsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Nexi", "models");
    }

    public string ModelsDirectory { get; }

    public IReadOnlyList<CatalogModelInfo> PredefinedCatalog { get; } = new List<CatalogModelInfo>
    {
        new("microsoft/Phi-3-mini-4k-instruct-gguf",
            "Phi-3 Mini 4K Instruct",
            "Microsoft's Phi-3 Mini 4K Instruct model optimized for efficient inference.",
            "3.8 GB", "FP16",
            "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-fp16.gguf",
            "microsoft/Phi-3-mini-4k-instruct-gguf"),
        new("bartowski/Meta-Llama-3.1-8B-Instruct-GGUF",
            "Meta Llama 3.1 8B Instruct",
            "Meta's Llama 3.1 8B instruction-tuned model for general-purpose tasks.",
            "8.6 GB", "Q8_0",
            "https://huggingface.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/Meta-Llama-3.1-8B-Instruct-Q8_0.gguf",
            "bartowski/Meta-Llama-3.1-8B-Instruct-GGUF"),
        new("Qwen/Qwen2-7B-Instruct-GGUF",
            "Qwen2 7B Instruct",
            "Qwen2 7B instruction-tuned model with 8-bit quantization for efficient inference.",
            "8.1 GB", "Q8_0",
            "https://huggingface.co/Qwen/Qwen2-7B-Instruct-GGUF/resolve/main/Qwen2-7B-Instruct-Q8_0.gguf",
            "Qwen/Qwen2-7B-Instruct-GGUF"),
        new("bartowski/Llama-3.2-3B-Instruct-GGUF",
            "Llama 3.2 3B Instruct",
            "Meta's lightweight Llama 3.2 3B instruction-tuned model - great for local use.",
            "2.0 GB", "Q4_K_M",
            "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf",
            "bartowski/Llama-3.2-3B-Instruct-GGUF"),
        new("MaziyarPanahi/Mistral-7B-Instruct-v0.3-GGUF",
            "Mistral 7B Instruct v0.3",
            "Mistral's 7B instruction-tuned model, Q4_K_M quantization.",
            "4.1 GB", "Q4_K_M",
            "https://huggingface.co/MaziyarPanahi/Mistral-7B-Instruct-v0.3-GGUF/resolve/main/Mistral-7B-Instruct-v0.3.Q4_K_M.gguf",
            "MaziyarPanahi/Mistral-7B-Instruct-v0.3-GGUF"),
    };

    public IReadOnlyList<(string Id, string FileName, string SizeText)> WhisperModelOptions { get; } =
        new List<(string, string, string)>
        {
            ("tiny", "ggml-tiny.bin", "~75 MB"),
            ("base", "ggml-base.bin", "~142 MB"),
            ("small", "ggml-small.bin", "~466 MB"),
        };

    public Task<bool> IsOllamaReachableAsync(CancellationToken ct = default) =>
        _ollama.IsServerReachableAsync(ct);

    public Task<IReadOnlyList<InstalledModelInfo>> ListOllamaModelsAsync(CancellationToken ct = default) =>
        _ollama.ListModelsAsync(ct);

    public async IAsyncEnumerable<ModelDownloadProgress> PullOllamaModelAsync(
        string name,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var progress in WithCancellation(name, ct, _ollama.PullModelAsync(name, ct)))
        {
            yield return progress;
        }
    }

    public Task DeleteOllamaModelAsync(string name, CancellationToken ct = default) =>
        _ollama.DeleteModelAsync(name, ct);

    public Task<IReadOnlyList<CatalogModelInfo>> SearchCatalogAsync(string query, CancellationToken ct = default) =>
        _huggingFace.SearchModelsAsync(query, ct);

    public async IAsyncEnumerable<ModelDownloadProgress> DownloadGgufAsync(
        CatalogModelInfo model,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var fileName = $"{model.RepoId.Replace('/', '-')}-{Path.GetFileName(new Uri(model.DownloadUrl).LocalPath)}";
        var destination = Path.Combine(ModelsDirectory, "gguf", fileName);

        await foreach (var progress in WithCancellation(model.Id, ct,
                           _huggingFace.DownloadFileAsync(model.DownloadUrl, destination, model.Id, ct)))
        {
            yield return progress;
        }
    }

    public IReadOnlyList<InstalledModelInfo> ListLocalGgufFiles()
    {
        var dir = Path.Combine(ModelsDirectory, "gguf");
        if (!Directory.Exists(dir))
        {
            return Array.Empty<InstalledModelInfo>();
        }

        return Directory.GetFiles(dir, "*.gguf")
            .Select(f => new InstalledModelInfo(
                Path.GetFileName(f),
                new FileInfo(f).Length,
                File.GetLastWriteTimeUtc(f),
                ModelSource.Gguf))
            .OrderBy(m => m.Name)
            .ToList();
    }

    public Task DeleteLocalGgufFileAsync(string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(ModelsDirectory, "gguf", fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ModelDownloadProgress> ImportGgufToOllamaAsync(
        string fileName,
        string modelName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var path = Path.Combine(ModelsDirectory, "gguf", fileName);
        await foreach (var progress in WithCancellation(modelName, ct, _ollama.ImportGgufAsync(modelName, path, ct)))
        {
            yield return progress;
        }
    }

    public IReadOnlyList<InstalledModelInfo> ListWhisperModels()
    {
        if (!Directory.Exists(ModelsDirectory))
        {
            return Array.Empty<InstalledModelInfo>();
        }

        return Directory.GetFiles(ModelsDirectory, "ggml-*.bin")
            .Select(f => new InstalledModelInfo(
                Path.GetFileName(f),
                new FileInfo(f).Length,
                File.GetLastWriteTimeUtc(f),
                ModelSource.Whisper))
            .OrderBy(m => m.Name)
            .ToList();
    }

    public async IAsyncEnumerable<ModelDownloadProgress> DownloadWhisperModelAsync(
        string optionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var option = WhisperModelOptions.FirstOrDefault(o => o.Id == optionId);
        if (option.Id is null)
        {
            throw new ArgumentException($"Unknown Whisper model option: {optionId}", nameof(optionId));
        }

        var type = option.Id switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            _ => GgmlType.Tiny
        };

        var destination = Path.Combine(ModelsDirectory, option.FileName);
        var partPath = destination + ".part";

        await foreach (var progress in WithCancellation(option.Id, ct, DownloadWhisperStreamAsync(type, partPath, option.FileName, ct)))
        {
            yield return progress;
        }

        File.Move(partPath, destination, overwrite: true);
        yield return new ModelDownloadProgress(option.Id, 100, "done", new FileInfo(destination).Length, new FileInfo(destination).Length);
    }

    private async IAsyncEnumerable<ModelDownloadProgress> DownloadWhisperStreamAsync(
        GgmlType type,
        string partPath,
        string modelId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        Directory.CreateDirectory(ModelsDirectory);
        var stream = _whisperModelStream is not null
            ? await _whisperModelStream(type, ct)
            : await Whisper.net.Ggml.WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(type, QuantizationType.NoQuantization, ct);
        await using var modelStream = stream;
        var total = modelStream.CanSeek ? modelStream.Length : -1;
        long received = 0;
        var lastPercent = -1;

        await using var target = File.Create(partPath);
        var buffer = new byte[81920];
        int read;
        while ((read = await modelStream.ReadAsync(buffer, ct)) > 0)
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

    public Task DeleteWhisperModelAsync(string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(ModelsDirectory, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public bool CancelDownload(string modelId)
    {
        if (_downloads.TryGetValue(modelId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    private async IAsyncEnumerable<ModelDownloadProgress> WithCancellation(
        string modelId,
        CancellationToken ct,
        IAsyncEnumerable<ModelDownloadProgress> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken outerCt = default)
    {
        var effective = outerCt.IsCancellationRequested ? outerCt : ct;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(effective);
        _downloads[modelId] = linked;
        try
        {
            await foreach (var progress in source.WithCancellation(linked.Token))
            {
                yield return progress;
            }
        }
        finally
        {
            _logger.LogDebug("Download of {ModelId} finished or was cancelled", modelId);
            _downloads.TryRemove(modelId, out _);
        }
    }
}
