using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nexi.UI.ViewModels;

/// <summary>Shell view model: sidebar navigation between the Chat, History, Models and Settings pages.</summary>
public partial class MainViewModel : ViewModelBase
{
    private const double ExpandedWidth = 250;
    private const double CollapsedWidth = 60;

    private readonly ChatViewModel _chat;
    private readonly ChatHistoryViewModel _history;
    private readonly ModelsViewModel _models;
    private readonly SettingsViewModel _settings;
    private readonly Func<ChatViewModel> _chatFactory;

    public MainViewModel(
        ChatViewModel chat,
        ChatHistoryViewModel history,
        ModelsViewModel models,
        SettingsViewModel settings,
        Func<ChatViewModel> chatFactory)
    {
        _chat = chat;
        _history = history;
        _models = models;
        _settings = settings;
        _chatFactory = chatFactory;
        _currentPage = chat;
    }

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public double SidebarWidth => IsSidebarExpanded ? ExpandedWidth : CollapsedWidth;

    partial void OnIsSidebarExpandedChanged(bool value) => OnPropertyChanged(nameof(SidebarWidth));

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    [RelayCommand]
    private void NewChat() => CurrentPage = _chatFactory();

    [RelayCommand]
    private void ChatHistory() => CurrentPage = _history;

    [RelayCommand]
    private void Models() => CurrentPage = _models;

    [RelayCommand]
    private void Settings() => CurrentPage = _settings;
}
