namespace Nexi.Services.Speech;

/// <summary>Fallback used when voice input is not available on this machine.</summary>
public sealed class NullSpeechRecognitionService : ISpeechRecognitionService
{
    public bool IsAvailable => false;

    public string Status { get; init; } = "Voice input is not available on this machine.";

    public IReadOnlyList<string> GetAvailableInputDevices() => Array.Empty<string>();

    public Task<string?> RecognizeOnceAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>(null);
}
