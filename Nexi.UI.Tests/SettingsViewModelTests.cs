using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Data.Models;
using Nexi.UI.ViewModels;

namespace Nexi.UI.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public async Task Save_persists_all_settings()
    {
        var store = TestStore.Create();
        var vm = new SettingsViewModel(store, new UnavailableSpeech(), NullLogger<SettingsViewModel>.Instance);
        vm.Load();

        vm.SelectedProvider = AiProviderKind.Ollama;
        vm.Model = "mistral";
        vm.ApiKey = "sk-test";
        vm.BaseUrl = "http://localhost:11434";
        vm.SelectedInputDeviceIndex = 2;
        vm.SelectedSpeechModelFile = "ggml-base.bin";
        vm.SelectedTheme = "Light";

        await vm.SaveSettingsCommand.ExecuteAsync(null);

        var settings = await store.GetSettingsAsync();
        Assert.Equal(AiProviderKind.Ollama, settings.Provider);
        Assert.Equal("mistral", settings.Model);
        Assert.Equal("sk-test", settings.ApiKey);
        Assert.Equal("http://localhost:11434", settings.BaseUrl);
        Assert.Equal(2, settings.InputDeviceIndex);
        Assert.Equal("ggml-base.bin", settings.SpeechModelFile);
        Assert.Equal("Light", settings.Theme);
        Assert.Equal("Settings saved.", vm.StatusText);
    }

    [Fact]
    public async Task Load_reads_existing_settings_and_devices()
    {
        var store = TestStore.Create();
        await store.SaveSettingsAsync(new AppSettings
        {
            Provider = AiProviderKind.OpenAICompatible,
            Model = "gpt-4o-mini",
            BaseUrl = "https://api.openai.com/v1",
            Theme = "Dark"
        });

        var vm = new SettingsViewModel(store, new UnavailableSpeech(), NullLogger<SettingsViewModel>.Instance);
        vm.Load();

        Assert.Equal(AiProviderKind.OpenAICompatible, vm.SelectedProvider);
        Assert.Equal("gpt-4o-mini", vm.Model);
        Assert.Equal("Dark", vm.SelectedTheme);
        Assert.Single(vm.InputDevices); // fallback "Default"
        Assert.Equal("https://api.openai.com/v1", vm.BaseUrl); // default for the provider
    }

    [Fact]
    public void SetSpeechModels_exposes_file_names()
    {
        var vm = new SettingsViewModel(TestStore.Create(), new UnavailableSpeech(), NullLogger<SettingsViewModel>.Instance);

        vm.SetSpeechModels(new List<(string, string, string)> { ("tiny", "ggml-tiny.bin", "~75 MB") });

        Assert.Equal(new[] { "ggml-tiny.bin" }, vm.SpeechModelsFileNames);
    }
}
