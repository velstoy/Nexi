using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using Nexi.Data;
using Nexi.Data.Models;
using Nexi.Services.Speech;

namespace Nexi.UI.ViewModels;

/// <summary>The settings page: AI provider, voice input device and theme.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly INexiStore _store;
    private readonly ISpeechRecognitionService _speech;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(INexiStore store, ISpeechRecognitionService speech, ILogger<SettingsViewModel> logger)
    {
        _store = store;
        _speech = speech;
        _logger = logger;
    }

    public IReadOnlyList<AiProviderKind> Providers { get; } = Enum.GetValues<AiProviderKind>();

    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "System", "Dark", "Light" };

    public IReadOnlyList<string> InputDevices { get; private set; } = Array.Empty<string>();

    public IReadOnlyList<(string Id, string FileName, string SizeText)> SpeechModels { get; private set; } =
        Array.Empty<(string, string, string)>();

    [ObservableProperty]
    private AiProviderKind _selectedProvider = AiProviderKind.LocalAssistant;

    [ObservableProperty]
    private string _model = "llama3.2";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private int _selectedInputDeviceIndex = -1;

    [ObservableProperty]
    private string _selectedSpeechModelFile = "ggml-tiny.bin";

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    [ObservableProperty]
    private string _statusText = "Ready";

    public string ProviderDisplay => SelectedProvider switch
    {
        AiProviderKind.LocalAssistant => "Local assistant (offline)",
        AiProviderKind.Ollama => "Ollama (local models)",
        AiProviderKind.OpenAICompatible => "OpenAI-compatible (cloud)",
        _ => SelectedProvider.ToString()
    };

    partial void OnSelectedProviderChanged(AiProviderKind value)
    {
        OnPropertyChanged(nameof(ProviderDisplay));
        if (string.IsNullOrWhiteSpace(BaseUrl) ||
            BaseUrl == "http://localhost:11434" ||
            BaseUrl == "https://api.openai.com/v1")
        {
            BaseUrl = value == AiProviderKind.Ollama
                ? "http://localhost:11434"
                : value == AiProviderKind.OpenAICompatible
                    ? "https://api.openai.com/v1"
                    : string.Empty;
        }
    }

    public void Load()
    {
        try
        {
            var settings = _store.GetSettingsAsync().GetAwaiter().GetResult();
            SelectedProvider = settings.Provider;
            Model = settings.Model;
            ApiKey = settings.ApiKey ?? string.Empty;
            BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? DefaultBaseUrl(settings.Provider) : settings.BaseUrl;
            SelectedInputDeviceIndex = settings.InputDeviceIndex;
            SelectedSpeechModelFile = settings.SpeechModelFile;
            SelectedTheme = settings.Theme;

            InputDevices = _speech.GetAvailableInputDevices();
            if (InputDevices.Count == 0)
            {
                InputDevices = new[] { "Default" };
            }

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            StatusText = $"Could not load settings: {ex.Message}";
        }
    }

    public IEnumerable<string> SpeechModelsFileNames => SpeechModels.Select(o => o.FileName);

    public void SetSpeechModels(IReadOnlyList<(string Id, string FileName, string SizeText)> options)
    {
        SpeechModels = options;
        OnPropertyChanged(nameof(SpeechModelsFileNames));
        if (!SpeechModelsFileNames.Contains(SelectedSpeechModelFile))
        {
            SelectedSpeechModelFile = SpeechModelsFileNames.FirstOrDefault() ?? "ggml-tiny.bin";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new AppSettings
            {
                Provider = SelectedProvider,
                Model = string.IsNullOrWhiteSpace(Model) ? "llama3.2" : Model.Trim(),
                ApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim(),
                BaseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl(SelectedProvider) : BaseUrl.Trim(),
                InputDeviceIndex = SelectedInputDeviceIndex,
                SpeechModelFile = string.IsNullOrWhiteSpace(SelectedSpeechModelFile) ? "ggml-tiny.bin" : SelectedSpeechModelFile,
                Theme = SelectedTheme
            };
            await _store.SaveSettingsAsync(settings);
            App.ApplyTheme(SelectedTheme);
            StatusText = "Settings saved.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusText = $"Could not save settings: {ex.Message}";
        }
    }

    private static string DefaultBaseUrl(AiProviderKind kind) => kind switch
    {
        AiProviderKind.Ollama => "http://localhost:11434",
        AiProviderKind.OpenAICompatible => "https://api.openai.com/v1",
        _ => string.Empty
    };
}
