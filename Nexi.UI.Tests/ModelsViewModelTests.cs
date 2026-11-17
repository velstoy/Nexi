
using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Data.Models;
using Nexi.Services.Models;
using Nexi.UI.ViewModels;

namespace Nexi.UI.Tests;

public class ModelsViewModelTests
{
    private static (ModelsViewModel vm, FakeModelManager models, Nexi.Data.INexiStore store) Create()
    {
        var models = new FakeModelManager();
        var store = TestStore.Create();
        var vm = new ModelsViewModel(models, store, NullLogger<ModelsViewModel>.Instance);
        return (vm, models, store);
    }

    [Fact]
    public Task Refresh_lists_ollama_and_whisper_models_with_active_flags() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, store) = Create();
        models.OllamaModels.Add(new InstalledModelInfo("llama3.2", 2_000_000_000, DateTimeOffset.UtcNow, ModelSource.Ollama));
        models.LocalWhisperFiles.Add("ggml-tiny.bin");
        await store.SaveSettingsAsync(new AppSettings { Model = "llama3.2", SpeechModelFile = "ggml-tiny.bin" });

        await vm.RefreshModelsCommand.ExecuteAsync(null);

        var ollama = Assert.Single(vm.OllamaModels);
        Assert.Equal("llama3.2", ollama.Name);
        Assert.True(ollama.IsActive);
        Assert.True(vm.OllamaReachable);

        var whisper = Assert.Single(vm.WhisperModels, w => w.Id == "tiny");
        Assert.True(whisper.IsDownloaded);
        Assert.True(whisper.IsActive);
        Assert.False(vm.WhisperModels.First(w => w.Id == "base").IsDownloaded);
    });

    [Fact]
    public Task Install_pulls_model_by_name_and_refreshes() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        await vm.RefreshModelsCommand.ExecuteAsync(null);

        vm.NewModelName = "mistral";
        await vm.InstallOllamaModelCommand.ExecuteAsync(null);

        Assert.Contains("mistral", models.PulledModels);
        Assert.Contains("Installed mistral", vm.StatusMessage);
        Assert.Equal(string.Empty, vm.NewModelName);
    });

    [Fact]
    public Task Select_ollama_model_persists_provider_and_model() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, store) = Create();
        models.OllamaModels.Add(new InstalledModelInfo("phi4", 1, DateTimeOffset.UtcNow, ModelSource.Ollama));
        await vm.RefreshModelsCommand.ExecuteAsync(null);

        await vm.SelectOllamaModelCommand.ExecuteAsync(vm.OllamaModels[0]);

        var settings = await store.GetSettingsAsync();
        Assert.Equal(AiProviderKind.Ollama, settings.Provider);
        Assert.Equal("phi4", settings.Model);
    });

    [Fact]
    public Task Delete_ollama_model_calls_manager() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        models.OllamaModels.Add(new InstalledModelInfo("phi4", 1, DateTimeOffset.UtcNow, ModelSource.Ollama));
        await vm.RefreshModelsCommand.ExecuteAsync(null);

        await vm.DeleteOllamaModelCommand.ExecuteAsync(vm.OllamaModels[0]);

        Assert.Contains("phi4", models.DeletedOllama);
    });

    [Fact]
    public Task Download_catalog_model_marks_item_downloaded() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        await vm.RefreshModelsCommand.ExecuteAsync(null);
        var item = vm.CatalogModels[0];
        Assert.True(item.CanDownload);

        await vm.DownloadCatalogModelCommand.ExecuteAsync(item);

        Assert.True(item.IsDownloaded);
        Assert.False(item.CanDownload);
        Assert.Single(models.LocalGgufFiles);
    });

    [Fact]
    public Task Delete_gguf_removes_file_and_resets_state() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        await vm.RefreshModelsCommand.ExecuteAsync(null);
        var item = vm.CatalogModels[0];
        await vm.DownloadCatalogModelCommand.ExecuteAsync(item);

        await vm.DeleteGgufCommand.ExecuteAsync(item);

        Assert.False(item.IsDownloaded);
        Assert.Empty(models.LocalGgufFiles);
        Assert.Single(models.DeletedGguf);
    });

    [Fact]
    public Task Import_gguf_registers_in_ollama() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        await vm.RefreshModelsCommand.ExecuteAsync(null);
        var item = vm.CatalogModels[0];
        await vm.DownloadCatalogModelCommand.ExecuteAsync(item);

        await vm.ImportGgufCommand.ExecuteAsync(item);

        var import = Assert.Single(models.ImportedGguf);
        Assert.Contains(item.LocalFileName, import);
    });

    [Fact]
    public Task Search_catalog_appends_results() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        models.SearchResults.Add(new CatalogModelInfo(
            "Qwen/Qwen2-7B-Instruct-GGUF", "Qwen2 7B", "d", "8.1 GB", "Q8_0",
            "https://huggingface.co/Qwen/Qwen2-7B-Instruct-GGUF/resolve/main/Qwen2-7B-Instruct-Q8_0.gguf",
            "Qwen/Qwen2-7B-Instruct-GGUF"));
        await vm.RefreshModelsCommand.ExecuteAsync(null);
        var initialCount = vm.CatalogModels.Count;

        vm.CatalogSearchQuery = "qwen";
        await vm.SearchCatalogCommand.ExecuteAsync(null);

        Assert.Equal(initialCount + 1, vm.CatalogModels.Count);
        Assert.Contains(vm.CatalogModels, c => c.Info.Name == "Qwen2 7B");
    });

    [Fact]
    public Task Download_and_select_whisper_model() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, store) = Create();
        await vm.RefreshModelsCommand.ExecuteAsync(null);
        var baseModel = vm.WhisperModels.First(w => w.Id == "base");
        Assert.False(baseModel.IsDownloaded);

        await vm.DownloadWhisperCommand.ExecuteAsync(baseModel);
        Assert.True(baseModel.IsDownloaded);
        Assert.Contains("base", models.DownloadedWhisper);

        await vm.SelectWhisperCommand.ExecuteAsync(baseModel);
        var settings = await store.GetSettingsAsync();
        Assert.Equal("ggml-base.bin", settings.SpeechModelFile);
    });

    [Fact]
    public Task Cancel_download_forwards_to_manager() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        await vm.RefreshModelsCommand.ExecuteAsync(null);
        var item = vm.CatalogModels[0];

        vm.CancelDownloadCommand.Execute(item);

        Assert.Contains(item.Info.Id, models.CancelledDownloads);
    });

    [Fact]
    public Task Refresh_reports_unreachable_ollama() => HeadlessSetup.RunAsync(async () =>
    {
        var (vm, models, _) = Create();
        models.OllamaReachable = false;

        await vm.RefreshModelsCommand.ExecuteAsync(null);

        Assert.False(vm.OllamaReachable);
        Assert.Contains("not reachable", vm.StatusMessage);
    });
}
