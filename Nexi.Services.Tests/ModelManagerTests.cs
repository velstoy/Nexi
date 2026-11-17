using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Services.Models;

using Whisper.net.Ggml;

namespace Nexi.Services.Tests;

public class ModelManagerTests : IDisposable
{
    private readonly string _dir;
    private readonly FakeOllamaAdmin _ollama = new();
    private readonly FakeHuggingFaceClient _hf = new();
    private readonly ModelManager _manager;

    public ModelManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "nexi-model-tests-" + Guid.NewGuid().ToString("N"));
        _manager = new ModelManager(
            _ollama,
            _hf,
            TestStore.Create(),
            NullLogger<ModelManager>.Instance,
            modelsDirectory: _dir,
            whisperModelStream: (type, ct) => Task.FromResult<Stream>(new MemoryStream(new byte[500])));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task ListOllamaModels_returns_installed_models()
    {
        _ollama.Models.Add(new InstalledModelInfo("llama3.2", 2_000_000_000, DateTimeOffset.UtcNow, ModelSource.Ollama));

        var models = await _manager.ListOllamaModelsAsync();

        var model = Assert.Single(models);
        Assert.Equal("llama3.2", model.Name);
        Assert.Equal("1.86 GB", model.SizeText);
        Assert.Equal(ModelSource.Ollama, model.Source);
    }

    [Fact]
    public async Task PullOllamaModel_reports_progress_and_completes()
    {
        var progress = new List<ModelDownloadProgress>();
        await foreach (var p in _manager.PullOllamaModelAsync("mistral"))
        {
            progress.Add(p);
        }

        Assert.Contains("mistral", _ollama.Pulled);
        Assert.Equal(100, progress[^1].Percent);
        Assert.Contains(progress, p => p.Percent == 50);
        Assert.Contains(_ollama.Models, m => m.Name == "mistral");
    }

    [Fact]
    public async Task DeleteOllamaModel_removes_it()
    {
        _ollama.Models.Add(new InstalledModelInfo("phi4", 1, DateTimeOffset.UtcNow, ModelSource.Ollama));

        await _manager.DeleteOllamaModelAsync("phi4");

        Assert.Contains("phi4", _ollama.Deleted);
        Assert.Empty(_ollama.Models);
    }

    [Fact]
    public async Task DownloadGguf_saves_file_under_gguf_dir_and_lists_it()
    {
        var catalog = new CatalogModelInfo(
            "bartowski/Llama-3.2-3B-Instruct-GGUF",
            "Llama 3.2 3B Instruct",
            "desc",
            "2.0 GB",
            "Q4_K_M",
            "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf",
            "bartowski/Llama-3.2-3B-Instruct-GGUF");

        var last = default(ModelDownloadProgress);
        await foreach (var p in _manager.DownloadGgufAsync(catalog))
        {
            last = p;
        }

        Assert.Equal(100, last!.Percent);
        var file = Assert.Single(_manager.ListLocalGgufFiles());
        Assert.Equal("bartowski-Llama-3.2-3B-Instruct-GGUF-Llama-3.2-3B-Instruct-Q4_K_M.gguf", file.Name);
        Assert.Equal(ModelSource.Gguf, file.Source);
        Assert.True(File.Exists(Path.Combine(_dir, "gguf", file.Name)));

        await _manager.DeleteLocalGgufFileAsync(file.Name);
        Assert.Empty(_manager.ListLocalGgufFiles());
    }

    [Fact]
    public async Task ImportGguf_registers_model_in_ollama()
    {
        var catalog = _manager.PredefinedCatalog[0];
        await foreach (var _ in _manager.DownloadGgufAsync(catalog)) { }

        var ggufFile = _manager.ListLocalGgufFiles()[0].Name;
        await foreach (var _ in _manager.ImportGgufToOllamaAsync(ggufFile, "test-model")) { }

        var import = Assert.Single(_ollama.Imported);
        Assert.Contains("test-model", import);
        Assert.Contains(ggufFile, import);
    }

    [Fact]
    public async Task DownloadWhisperModel_writes_model_file_and_lists_it()
    {
        var last = default(ModelDownloadProgress);
        await foreach (var p in _manager.DownloadWhisperModelAsync("tiny"))
        {
            last = p;
        }

        Assert.Equal(100, last!.Percent);
        var whisper = Assert.Single(_manager.ListWhisperModels());
        Assert.Equal("ggml-tiny.bin", whisper.Name);
        Assert.Equal(ModelSource.Whisper, whisper.Source);

        await _manager.DeleteWhisperModelAsync("ggml-tiny.bin");
        Assert.Empty(_manager.ListWhisperModels());
    }

    [Fact]
    public async Task DownloadWhisperModel_unknown_option_throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in _manager.DownloadWhisperModelAsync("huge-v99")) { }
        });
    }

    [Fact]
    public async Task CancelDownload_cancels_running_pull()
    {
        _ollama.PullDelay = TimeSpan.FromSeconds(30);
        using var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            await foreach (var _ in _manager.PullOllamaModelAsync("slow-model", cts.Token)) { }
        });

        // Poll until the download registers itself, instead of relying on a fixed delay.
        var cancelled = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && !cancelled)
        {
            cancelled = _manager.CancelDownload("slow-model");
            if (!cancelled)
            {
                await Task.Delay(20);
            }
        }

        Assert.True(cancelled, "The running download was registered and could be cancelled.");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        cts.Cancel();
    }

    [Fact]
    public void PredefinedCatalog_contains_known_models_with_download_urls()
    {
        Assert.Contains(_manager.PredefinedCatalog, m => m.Name.Contains("Phi-3"));
        Assert.Contains(_manager.PredefinedCatalog, m => m.Name.Contains("Llama"));
        Assert.All(_manager.PredefinedCatalog, m =>
        {
            Assert.StartsWith("https://huggingface.co/", m.DownloadUrl);
            Assert.EndsWith(".gguf", m.DownloadUrl);
        });
    }
}
