using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Nexi.UI.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

/// <summary>Clipboard access via the main window of the classic desktop lifetime.</summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.Clipboard;
        return clipboard?.SetTextAsync(text) ?? Task.CompletedTask;
    }
}
