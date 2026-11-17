using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using Nexi.Data;
using Nexi.Data.Models;

namespace Nexi.UI.ViewModels;

public partial class ChatHistoryItemViewModel : ViewModelBase
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Preview { get; init; } = string.Empty;

    public DateTime Timestamp { get; init; }

    public string CommandText { get; init; } = string.Empty;

    public bool Executed { get; init; }
}

/// <summary>The chat history page: searchable list of past commands with delete support.</summary>
public partial class ChatHistoryViewModel : ViewModelBase
{
    private readonly INexiStore _store;
    private readonly ILogger<ChatHistoryViewModel> _logger;

    public ChatHistoryViewModel(INexiStore store, ILogger<ChatHistoryViewModel> logger)
    {
        _store = store;
        _logger = logger;
    }

    public ObservableCollection<ChatHistoryItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    public void Load() => _ = RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var entries = await _store.GetRecentHistoryAsync(200);
            Items.Clear();
            foreach (var entry in entries)
            {
                Items.Add(ToItem(entry));
            }

            ApplyFilter();
            StatusText = $"{Items.Count} entr{(Items.Count == 1 ? "y" : "ies")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
            StatusText = $"Could not load history: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ChatHistoryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _store.DeleteHistoryEntryAsync(item.Id);
            Items.Remove(item);
            ApplyFilter();
            StatusText = $"{Items.Count} entr{(Items.Count == 1 ? "y" : "ies")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete history entry");
            StatusText = $"Could not delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        try
        {
            await _store.ClearHistoryAsync();
            Items.Clear();
            ApplyFilter();
            StatusText = "History cleared.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history");
            StatusText = $"Could not clear: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchQuery = string.Empty;

    private void ApplyFilter()
    {
        // Rebuild a filtered view by toggling visibility is awkward with ItemsControl;
        // instead expose filtered items through a view in the XAML via CollectionViewSource-like filtering.
        FilteredItems.Clear();
        var query = SearchQuery.Trim();
        foreach (var item in Items)
        {
            if (query.Length == 0 ||
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Preview.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredItems.Add(item);
            }
        }

        OnPropertyChanged(nameof(FilteredItems));
    }

    public ObservableCollection<ChatHistoryItemViewModel> FilteredItems { get; } = new();

    private static ChatHistoryItemViewModel ToItem(CommandHistoryEntry entry) => new()
    {
        Id = entry.Id,
        Title = Truncate(entry.Input, 60),
        Preview = Truncate(entry.AiReply.ReplaceLineEndings(" "), 120),
        Timestamp = entry.Timestamp,
        CommandText = entry.CommandKind == CommandKind.None
            ? "chat"
            : $"{entry.CommandKind}{(entry.Executed ? (entry.Success ? " - done" : " - failed") : "")}",
        Executed = entry.Executed
    };

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "...";
}
