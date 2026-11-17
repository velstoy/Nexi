using System.Text;

using Microsoft.Extensions.Logging;

using NAudio.Wave;

using Nexi.Data;

using Whisper.net;
using Whisper.net.Ggml;

namespace Nexi.Services.Speech;

/// <summary>
/// Local, privacy-friendly speech recognition: captures the selected microphone with NAudio
/// (Windows) and transcribes it with Whisper.net using the model file chosen in settings.
/// </summary>
public sealed class WhisperSpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private static readonly TimeSpan MaxUtterance = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SilenceToStop = TimeSpan.FromMilliseconds(1500);
    private const float SpeechThreshold = 0.02f;

    private readonly INexiStore _store;
    private readonly ILogger<WhisperSpeechRecognitionService> _logger;
    private readonly string _modelsDirectory;
    private readonly SemaphoreSlim _modelLock = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loadedModelFile;

    public WhisperSpeechRecognitionService(
        INexiStore store,
        ILogger<WhisperSpeechRecognitionService> logger,
        string? modelsDirectory = null)
    {
        _store = store;
        _logger = logger;
        _modelsDirectory = modelsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Nexi", "models");
    }

    public bool IsAvailable => OperatingSystem.IsWindows() && WaveIn.DeviceCount > 0;

    public string Status => IsAvailable
        ? "Voice input: Whisper.net (local, offline)"
        : "Voice input unavailable: requires Windows with a microphone.";

    public IReadOnlyList<string> GetAvailableInputDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<string>();
        }

        var devices = new List<string>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            devices.Add(WaveIn.GetCapabilities(i).ProductName);
        }

        return devices;
    }

    public async Task<string?> RecognizeOnceAsync(CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var settings = await _store.GetSettingsAsync(ct);
        var deviceIndex = settings.InputDeviceIndex;

        var samples = await CaptureUntilSilenceAsync(deviceIndex, ct);
        if (samples is null || samples.Length == 0)
        {
            return null;
        }

        await using var processor = await CreateProcessorAsync(settings.SpeechModelFile, ct);
        var text = new StringBuilder();
        await foreach (var segment in processor.ProcessAsync(samples, ct))
        {
            text.Append(segment.Text);
        }

        var result = text.ToString().Trim();
        _logger.LogInformation("Recognized speech: {Text}", result);
        return result.Length == 0 ? null : result;
    }

    private async Task<float[]?> CaptureUntilSilenceAsync(int deviceIndex, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var format = new WaveFormat(16000, 16, 1);
        using var waveIn = new WaveIn
        {
            WaveFormat = format,
            BufferMilliseconds = 50,
            DeviceNumber = deviceIndex >= 0 ? deviceIndex : -1
        };

        var pcm = new List<byte>(16000 * 2 * 15);
        var speechDetected = false;
        var lastSpeech = DateTime.MinValue;
        var started = DateTime.UtcNow;
        var done = new TaskCompletionSource();

        waveIn.DataAvailable += (_, args) =>
        {
            var rms = ComputeRms(args.Buffer, args.BytesRecorded);
            pcm.AddRange(args.Buffer.Take(args.BytesRecorded));

            if (rms > SpeechThreshold)
            {
                speechDetected = true;
                lastSpeech = DateTime.UtcNow;
            }

            if (speechDetected && DateTime.UtcNow - lastSpeech > SilenceToStop)
            {
                done.TrySetResult();
            }

            if (DateTime.UtcNow - started > MaxUtterance)
            {
                done.TrySetResult();
            }
        };

        waveIn.StartRecording();
        await using var registration = ct.Register(() => done.TrySetResult());
        await done.Task;
        waveIn.StopRecording();

        return Pcm16ToFloat(pcm.ToArray());
    }

    private async Task<WhisperProcessor> CreateProcessorAsync(string modelFile, CancellationToken ct)
    {
        await _modelLock.WaitAsync(ct);
        try
        {
            var modelPath = Path.Combine(_modelsDirectory, modelFile);
            if (_factory is null || _loadedModelFile != modelFile)
            {
                _factory?.Dispose();
                await EnsureModelDownloadedAsync(modelFile, ct);
                _factory = WhisperFactory.FromPath(modelPath);
                _loadedModelFile = modelFile;
            }

            return _factory.CreateBuilder()
                .WithLanguage("en")
                .Build();
        }
        finally
        {
            _modelLock.Release();
        }
    }

    private async Task EnsureModelDownloadedAsync(string modelFile, CancellationToken ct)
    {
        var modelPath = Path.Combine(_modelsDirectory, modelFile);
        if (File.Exists(modelPath))
        {
            return;
        }

        var type = modelFile.Contains("base", StringComparison.OrdinalIgnoreCase) ? GgmlType.Base
            : modelFile.Contains("small", StringComparison.OrdinalIgnoreCase) ? GgmlType.Small
            : GgmlType.Tiny;

        _logger.LogInformation("Downloading Whisper model {File} to {Path}", modelFile, modelPath);
        Directory.CreateDirectory(_modelsDirectory);
        var tempPath = modelPath + ".download";
        await using (var stream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
                         type, QuantizationType.NoQuantization, ct))
        await using (var file = File.Create(tempPath))
        {
            await stream.CopyToAsync(file, ct);
        }

        File.Move(tempPath, modelPath, overwrite: true);
    }

    private static float ComputeRms(byte[] buffer, int bytesRecorded)
    {
        long sum = 0;
        var samples = bytesRecorded / 2;
        for (var i = 0; i + 1 < bytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(buffer, i) / 32768f;
            sum += (long)(sample * sample * 1000);
        }

        return samples == 0 ? 0 : (float)Math.Sqrt(sum / (double)samples / 1000);
    }

    private static float[] Pcm16ToFloat(byte[] pcm)
    {
        var result = new float[pcm.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
        }

        return result;
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _modelLock.Dispose();
    }
}
