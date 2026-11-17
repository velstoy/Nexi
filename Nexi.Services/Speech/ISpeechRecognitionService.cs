namespace Nexi.Services.Speech;

/// <summary>Speech-to-text for voice commands. May be unavailable (no mic, no model, unsupported OS).</summary>
public interface ISpeechRecognitionService
{
    bool IsAvailable { get; }

    /// <summary>Human-readable reason when unavailable.</summary>
    string Status { get; }

    /// <summary>Names of connected microphones (empty when unavailable).</summary>
    IReadOnlyList<string> GetAvailableInputDevices();

    /// <summary>Listens once (until silence or cancellation) and returns the recognized text, or null.</summary>
    Task<string?> RecognizeOnceAsync(CancellationToken ct = default);
}
