using Nexi.Data.Models;
using Nexi.Services.Models;
using Nexi.Services.Pipeline;
using Nexi.Services.Speech;
using Nexi.UI.Services;

namespace Nexi.UI.Tests;

public sealed class FakeAssistant : INexiAssistant
{
    public Queue<AssistantResult> Results { get; } = new();

    public DesktopCommand? PendingCommand { get; private set; }

    public int ConfirmCalls { get; private set; }

    public int DismissCalls { get; private set; }

    public Task<AssistantResult> ProcessInputAsync(string input, CancellationToken ct = default)
    {
        PendingCommand = Results.Count > 0 && Results.Peek().Command is { Safety: CommandSafety.NeedsConfirmation }
            ? Results.Peek().Command
            : null;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new AssistantResult { Reply = "ok" });
    }

    public Task<AssistantResult> ConfirmPendingAsync(CancellationToken ct = default)
    {
        ConfirmCalls++;
        PendingCommand = null;
        return Task.FromResult(new AssistantResult { Reply = "ran it" });
    }

    public void DismissPending()
    {
        DismissCalls++;
        PendingCommand = null;
    }
}

public sealed class UnavailableSpeech : ISpeechRecognitionService
{
    public bool IsAvailable => false;
    public string Status => "Voice input is not available on this machine.";
    public IReadOnlyList<string> GetAvailableInputDevices() => new[] { "Default" };
    public Task<string?> RecognizeOnceAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
}

public sealed class FakeClipboard : IClipboardService
{
    public string? LastText { get; private set; }

    public Task SetTextAsync(string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }
}

public sealed class FakeModelManager : IModelManager
{
    public string ModelsDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "nexi-fake-models-" + Guid.NewGuid().ToString("N"));

    public IReadOnlyList<CatalogModelInfo> PredefinedCatalog { get; } = new List<CatalogModelInfo>
    {
        new("bartowski/Llama-3.2-3B-Instruct-GGUF", "Llama 3.2 3B Instruct", "desc", "2.0 GB", "Q4_K_M",
            "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf",
            "bartowski/Llama-3.2-3B-Instruct-GGUF")
    };

    public IReadOnlyList<(string Id, string FileName, string SizeText)> WhisperModelOptions { get; } =
        new List<(string, string, string)> { ("tiny", "ggml-tiny.bin", "~75 MB"), ("base", "ggml-base.bin", "~142 MB") };

    public bool OllamaReachable { get; set; } = true;

    public List<InstalledModelInfo> OllamaModels { get; } = new();

    public List<string> PulledModels { get; } = new();

    public List<string> DeletedOllama { get; } = new();

    public List<string> DeletedGguf { get; } = new();

    public List<string> DeletedWhisper { get; } = new();

    public List<string> ImportedGguf { get; } = new();

    public List<string> DownloadedWhisper { get; } = new();

    public List<CatalogModelInfo> SearchResults { get; } = new();

    public HashSet<string> LocalGgufFiles { get; } = new();

    public HashSet<string> LocalWhisperFiles { get; } = new();

    public List<string> CancelledDownloads { get; } = new();

    public Task<bool> IsOllamaReachableAsync(CancellationToken ct = default) => Task.FromResult(OllamaReachable);

    public Task<IReadOnlyList<InstalledModelInfo>> ListOllamaModelsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InstalledModelInfo>>(OllamaModels);

    public async IAsyncEnumerable<ModelDownloadProgress> PullOllamaModelAsync(
        string name,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        PulledModels.Add(name);
        yield return new ModelDownloadProgress(name, 50, "downloading", 500, 1000);
        yield return new ModelDownloadProgress(name, 100, "success", 1000, 1000);
        await Task.CompletedTask;
    }

    public Task DeleteOllamaModelAsync(string name, CancellationToken ct = default)
    {
        DeletedOllama.Add(name);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CatalogModelInfo>> SearchCatalogAsync(string query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CatalogModelInfo>>(SearchResults);

    public async IAsyncEnumerable<ModelDownloadProgress> DownloadGgufAsync(
        CatalogModelInfo model,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var fileName = $"{model.RepoId.Replace('/', '-')}-{Path.GetFileName(new Uri(model.DownloadUrl).LocalPath)}";
        var dir = Path.Combine(ModelsDirectory, "gguf");
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, fileName), new byte[100], ct);
        LocalGgufFiles.Add(fileName);
        yield return new ModelDownloadProgress(model.Id, 100, "done", 100, 100);
    }

    public IReadOnlyList<InstalledModelInfo> ListLocalGgufFiles() =>
        LocalGgufFiles.Select(f => new InstalledModelInfo(f, 100, DateTimeOffset.UtcNow, ModelSource.Gguf)).ToList();

    public Task DeleteLocalGgufFileAsync(string fileName, CancellationToken ct = default)
    {
        DeletedGguf.Add(fileName);
        LocalGgufFiles.Remove(fileName);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ModelDownloadProgress> ImportGgufToOllamaAsync(
        string fileName,
        string modelName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ImportedGguf.Add($"{modelName}:{fileName}");
        yield return new ModelDownloadProgress(modelName, 100, "success", 0, 0);
        await Task.CompletedTask;
    }

    public IReadOnlyList<InstalledModelInfo> ListWhisperModels() =>
        LocalWhisperFiles.Select(f => new InstalledModelInfo(f, 100, DateTimeOffset.UtcNow, ModelSource.Whisper)).ToList();

    public async IAsyncEnumerable<ModelDownloadProgress> DownloadWhisperModelAsync(
        string optionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var option = WhisperModelOptions.First(o => o.Id == optionId);
        Directory.CreateDirectory(ModelsDirectory);
        await File.WriteAllBytesAsync(Path.Combine(ModelsDirectory, option.FileName), new byte[100], ct);
        LocalWhisperFiles.Add(option.FileName);
        DownloadedWhisper.Add(optionId);
        yield return new ModelDownloadProgress(optionId, 100, "done", 100, 100);
    }

    public Task DeleteWhisperModelAsync(string fileName, CancellationToken ct = default)
    {
        DeletedWhisper.Add(fileName);
        LocalWhisperFiles.Remove(fileName);
        return Task.CompletedTask;
    }

    public bool CancelDownload(string modelId)
    {
        CancelledDownloads.Add(modelId);
        return true;
    }
}
