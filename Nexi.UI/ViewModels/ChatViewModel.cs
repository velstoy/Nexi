using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using Nexi.Data.Models;
using Nexi.Services.Pipeline;
using Nexi.Services.Speech;
using Nexi.UI.Services;

using ChatMessage = Nexi.UI.Models.ChatMessage;

namespace Nexi.UI.ViewModels;

/// <summary>The chat page: conversation with Nexi, voice input, command reference, confirmations.</summary>
public partial class ChatViewModel : ViewModelBase
{
    private readonly INexiAssistant _assistant;
    private readonly ISpeechRecognitionService _speech;
    private readonly IClipboardService _clipboard;
    private readonly ILogger<ChatViewModel> _logger;
    private CancellationTokenSource? _listeningCts;

    public ChatViewModel(
        INexiAssistant assistant,
        ISpeechRecognitionService speech,
        IClipboardService clipboard,
        ILogger<ChatViewModel> logger)
    {
        _assistant = assistant;
        _speech = speech;
        _clipboard = clipboard;
        _logger = logger;

        Messages.Add(ChatMessage.System(
            "Welcome to Nexi! Ask me to open a website, app or folder, search the web, " +
            "check the time or system info - or press the ? button for the command reference."));
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    /// <summary>Raised when a new message is added, so the view can scroll to the bottom.</summary>
    public event Action? ScrollToBottom;

    [ObservableProperty]
    private string _currentMessage = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isVoiceModeEnabled;

    [ObservableProperty]
    private bool _showCommandInstructions;

    [ObservableProperty]
    private bool _showPendingConfirmation;

    [ObservableProperty]
    private string _pendingText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    public bool HasMessageText => CurrentMessage.Trim().Length > 0;

    partial void OnCurrentMessageChanged(string value) => OnPropertyChanged(nameof(HasMessageText));

    partial void OnIsProcessingChanged(bool value) => OnPropertyChanged(nameof(HasMessageText));

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var input = CurrentMessage.Trim();
        if (input.Length == 0 || IsProcessing)
        {
            return;
        }

        CurrentMessage = string.Empty;
        IsProcessing = true;
        Messages.Add(ChatMessage.User(input));
        ScrollToBottom?.Invoke();

        try
        {
            var result = await _assistant.ProcessInputAsync(input);
            var badge = result.Command is null
                ? null
                : $"{result.Command.Kind} · {result.Command.Safety}";
            Messages.Add(ChatMessage.Assistant(result.Reply, badge));
            StatusText = $"Provider: {result.ProviderName}";

            if (result.RequiresConfirmation && result.Command is not null)
            {
                PendingText = $"Run this on your desktop? {result.Command.Explanation ?? result.Command.Argument}";
                ShowPendingConfirmation = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process input");
            Messages.Add(ChatMessage.System($"Something went wrong: {ex.Message}"));
        }
        finally
        {
            IsProcessing = false;
            ScrollToBottom?.Invoke();
        }
    }

    [RelayCommand]
    private async Task ToggleVoiceAsync()
    {
        if (!_speech.IsAvailable)
        {
            StatusText = _speech.Status;
            IsVoiceModeEnabled = false;
            return;
        }

        if (IsVoiceModeEnabled)
        {
            _listeningCts?.Cancel();
            IsVoiceModeEnabled = false;
            StatusText = "Ready";
            return;
        }

        IsVoiceModeEnabled = true;
        _listeningCts = new CancellationTokenSource();
        StatusText = "Listening...";

        try
        {
            while (IsVoiceModeEnabled && !_listeningCts.IsCancellationRequested)
            {
                var text = await _speech.RecognizeOnceAsync(_listeningCts.Token);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    CurrentMessage = string.IsNullOrWhiteSpace(CurrentMessage) ? text : $"{CurrentMessage} {text}";
                    IsVoiceModeEnabled = false;
                    await SendMessageAsync();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // user toggled voice off
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice recognition failed");
            StatusText = $"Voice error: {ex.Message}";
        }
        finally
        {
            IsVoiceModeEnabled = false;
            if (StatusText == "Listening...")
            {
                StatusText = "Ready";
            }
        }
    }

    [RelayCommand]
    private void ClearMessage() => CurrentMessage = string.Empty;

    [RelayCommand]
    private async Task CopyToClipboardAsync(string? content)
    {
        if (!string.IsNullOrEmpty(content))
        {
            await _clipboard.SetTextAsync(content);
            StatusText = "Copied to clipboard.";
        }
    }

    [RelayCommand]
    private void ToggleCommandInstructions(bool show) => ShowCommandInstructions = show;

    [RelayCommand]
    private void HideCommandInstructions() => ShowCommandInstructions = false;

    [RelayCommand]
    private async Task ConfirmPendingAsync()
    {
        ShowPendingConfirmation = false;
        IsProcessing = true;
        try
        {
            var result = await _assistant.ConfirmPendingAsync();
            Messages.Add(ChatMessage.Assistant(result.Reply, result.Command is null ? null : $"{result.Command.Kind}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm pending command");
            Messages.Add(ChatMessage.System($"Something went wrong: {ex.Message}"));
        }
        finally
        {
            IsProcessing = false;
            ScrollToBottom?.Invoke();
        }
    }

    [RelayCommand]
    private void DeclinePending()
    {
        _assistant.DismissPending();
        ShowPendingConfirmation = false;
        Messages.Add(ChatMessage.System("Command dismissed - nothing was run."));
        ScrollToBottom?.Invoke();
    }
}
