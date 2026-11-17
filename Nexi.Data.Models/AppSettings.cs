namespace Nexi.Data.Models;

/// <summary>Persisted application settings (single-row table).</summary>
public sealed class AppSettings
{
    public int Id { get; set; } = 1;

    public AiProviderKind Provider { get; set; } = AiProviderKind.LocalAssistant;

    /// <summary>Model name used by the selected provider (e.g. llama3.2, gpt-4o-mini).</summary>
    public string Model { get; set; } = "llama3.2";

    /// <summary>API key for cloud providers. Never logged.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Endpoint URL. Ollama default is http://localhost:11434; OpenAI-compatible default is https://api.openai.com/v1.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    public bool VoiceEnabled { get; set; }

    /// <summary>Index of the microphone to use for voice input (-1 = system default).</summary>
    public int InputDeviceIndex { get; set; } = -1;

    /// <summary>Whisper model file used for speech recognition (ggml-tiny.bin, ggml-base.bin, ...).</summary>
    public string SpeechModelFile { get; set; } = "ggml-tiny.bin";

    /// <summary>UI theme: System, Dark or Light.</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>When true, commands classified as Safe run without asking.</summary>
    public bool AutoConfirmSafeCommands { get; set; } = true;

    public int HistoryLimit { get; set; } = 200;
}
