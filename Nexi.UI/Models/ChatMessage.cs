using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexi.UI.Models;

/// <summary>One chat bubble in the conversation.</summary>
public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private bool _isUser;

    [ObservableProperty]
    private bool _isSystemMessage;

    /// <summary>Optional badge showing which desktop command the message triggered.</summary>
    [ObservableProperty]
    private string _commandBadge = string.Empty;

    public bool HasCommandBadge => CommandBadge.Length > 0;

    partial void OnCommandBadgeChanged(string value) => OnPropertyChanged(nameof(HasCommandBadge));

    public static ChatMessage User(string content) =>
        new() { Content = content, IsUser = true, Timestamp = DateTime.Now };

    public static ChatMessage Assistant(string content, string? commandBadge = null) =>
        new() { Content = content, IsUser = false, CommandBadge = commandBadge ?? string.Empty, Timestamp = DateTime.Now };

    public static ChatMessage System(string content) =>
        new() { Content = content, IsSystemMessage = true, Timestamp = DateTime.Now };
}
