using System.Collections.ObjectModel;

using Avalonia.Media;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using Nexi.Data;
using Nexi.Services.Models;

namespace Nexi.UI.ViewModels;

public partial class OllamaModelItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sizeText = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isBusy;
}

/// <summary>A GGUF catalog entry with download state.</summary>
public partial class CatalogModelItem : ViewModelBase
{
    private static readonly IBrush NotDownloadedBrush = new SolidColorBrush(Color.Parse("#e74c3c"));
    private static readonly IBrush DownloadingBrush = new SolidColorBrush(Color.Parse("#3498db"));
    private static readonly IBrush DownloadedBrush = new SolidColorBrush(Color.Parse("#2ecc71"));

    public CatalogModelItem(CatalogModelInfo info, bool isDownloaded, string localFileName)
    {
        Info = info;
        _localFileName = localFileName;
        IsDownloaded = isDownloaded;
    }

    public CatalogModelInfo Info { get; }

    [ObservableProperty]
    private string _localFileName = string.Empty;

    [ObservableProperty]
    private string _status = "Not Downloaded";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isDownloaded;

    public IBrush StatusBrush => IsDownloading ? DownloadingBrush : IsDownloaded ? DownloadedBrush : NotDownloadedBrush;

    public bool CanDownload => !IsDownloading && !IsDownloaded;

    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(CanDownload));
        Status = value ? "Downloading" : IsDownloaded ? "Downloaded" : "Not Downloaded";
    }

    partial void OnIsDownloadedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(CanDownload));
        Status = IsDownloading ? "Downloading" : value ? "Downloaded" : "Not Downloaded";
    }
}

/// <summary>A Whisper speech model with download state.</summary>
public partial class WhisperModelItem : ViewModelBase
{
    private static readonly IBrush NotDownloadedBrush = new SolidColorBrush(Color.Parse("#e74c3c"));
    private static readonly IBrush DownloadingBrush = new SolidColorBrush(Color.Parse("#3498db"));
    private static readonly IBrush DownloadedBrush = new SolidColorBrush(Color.Parse("#2ecc71"));

    public WhisperModelItem(string id, string fileName, string sizeText, bool isDownloaded, bool isActive)
    {
        Id = id;
        FileName = fileName;
        SizeText = sizeText;
        IsDownloaded = isDownloaded;
        IsActive = isActive;
    }

    public string Id { get; }

    public string FileName { get; }

    public string SizeText { get; }

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _progress;

    public IBrush StatusBrush => IsDownloading ? DownloadingBrush : IsDownloaded ? DownloadedBrush : NotDownloadedBrush;

    partial void OnIsDownloadingChanged(bool value) => OnPropertyChanged(nameof(StatusBrush));

    partial void OnIsDownloadedChanged(bool value) => OnPropertyChanged(nameof(StatusBrush));
}

/// <summary>
/// The Models page: install and manage Ollama models, download GGUF models from HuggingFace,
/// and manage Whisper speech models.
/// </summary>
public partial class ModelsViewModel : ViewModelBase
{
    private readonly IModelManager _models;
    private readonly INexiStore _store;
    private readonly ILogger<ModelsViewModel> _logger;

    public ModelsViewModel(IModelManager models, INexiStore store, ILogger<ModelsViewModel> logger)
    {
        _models = models;
        _store = store;
        _logger = logger;

        foreach (var model in _models.PredefinedCatalog)
        {
            CatalogModels.Add(new CatalogModelItem(model, isDownloaded: false, localFileName: string.Empty));
        }
    }

    public ObservableCollection<OllamaModelItem> OllamaModels { get; } = new();

    public ObservableCollection<CatalogModelItem> CatalogModels { get; } = new();

    public ObservableCollection<WhisperModelItem> WhisperModels { get; } = new();

    [ObservableProperty]
    private string _newModelName = string.Empty;

    [ObservableProperty]
    private string _catalogSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _ollamaReachable;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public void Load() => _ = RefreshModelsAsync();

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        IsLoading = true;
        ClearMessages();
        try
        {
            OllamaReachable = await _models.IsOllamaReachableAsync();
            var settings = await _store.GetSettingsAsync();

            OllamaModels.Clear();
            if (OllamaReachable)
            {
                foreach (var model in await _models.ListOllamaModelsAsync())
                {
                    OllamaModels.Add(new OllamaModelItem
                    {
                        Name = model.Name,
                        SizeText = model.SizeText,
                        IsActive = string.Equals(model.Name, settings.Model, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            var localGguf = _models.ListLocalGgufFiles().Select(f => f.Name).ToHashSet();
            foreach (var item in CatalogModels)
            {
                var fileName = ExpectedGgufFileName(item.Info);
                var downloaded = localGguf.Contains(fileName);
                item.LocalFileName = fileName;
                item.IsDownloaded = downloaded;
            }

            var whisperInstalled = _models.ListWhisperModels().Select(w => w.Name).ToHashSet();
            WhisperModels.Clear();
            foreach (var option in _models.WhisperModelOptions)
            {
                WhisperModels.Add(new WhisperModelItem(
                    option.Id,
                    option.FileName,
                    option.SizeText,
                    isDownloaded: whisperInstalled.Contains(option.FileName),
                    isActive: string.Equals(option.FileName, settings.SpeechModelFile, StringComparison.OrdinalIgnoreCase)));
            }

            StatusMessage = OllamaReachable
                ? $"{OllamaModels.Count} Ollama model(s), {localGguf.Count} GGUF file(s), {whisperInstalled.Count} speech model(s)"
                : "Ollama server not reachable at the configured endpoint - installs will fail until it runs.";
            HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh models");
            ErrorMessage = $"Error refreshing models: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task InstallOllamaModelAsync()
    {
        var name = NewModelName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        ClearMessages();
        var item = new OllamaModelItem { Name = name, IsBusy = true, SizeText = "installing..." };
        OllamaModels.Add(item);
        try
        {
            await foreach (var progress in _models.PullOllamaModelAsync(name))
            {
                PostToUi(() =>
                {
                    item.SizeText = progress.TotalBytes > 0
                        ? $"{InstalledModelInfo.FormatSize(progress.BytesReceived)} / {InstalledModelInfo.FormatSize(progress.TotalBytes)} ({progress.Percent}%)"
                        : $"{progress.Status} {progress.Percent}%";
                });
            }

            await RefreshModelsAsync();
            StatusMessage = $"Installed {name}.";
            HasStatusMessage = true;
            NewModelName = string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Install of {name} was cancelled.";
            HasStatusMessage = true;
            await RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install Ollama model {Model}", name);
            ErrorMessage = $"Error installing {name}: {ex.Message}";
            HasError = true;
            OllamaModels.Remove(item);
        }
    }

    [RelayCommand]
    private async Task DeleteOllamaModelAsync(OllamaModelItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _models.DeleteOllamaModelAsync(item.Name);
            StatusMessage = $"Deleted {item.Name}.";
            HasStatusMessage = true;
            await RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete model {Model}", item.Name);
            ErrorMessage = $"Error deleting {item.Name}: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task SelectOllamaModelAsync(OllamaModelItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var settings = await _store.GetSettingsAsync();
            settings.Provider = Data.Models.AiProviderKind.Ollama;
            settings.Model = item.Name;
            await _store.SaveSettingsAsync(settings);
            StatusMessage = $"{item.Name} is now the active model.";
            HasStatusMessage = true;
            await RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select model {Model}", item.Name);
            ErrorMessage = $"Error selecting model: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task SearchCatalogAsync()
    {
        var query = CatalogSearchQuery.Trim();
        if (query.Length == 0)
        {
            return;
        }

        ClearMessages();
        IsLoading = true;
        try
        {
            var results = await _models.SearchCatalogAsync(query);
            var localGguf = _models.ListLocalGgufFiles().Select(f => f.Name).ToHashSet();
            foreach (var model in results)
            {
                if (CatalogModels.Any(c => c.Info.Id == model.Id))
                {
                    continue;
                }

                var fileName = ExpectedGgufFileName(model);
                CatalogModels.Add(new CatalogModelItem(model, localGguf.Contains(fileName), fileName));
            }

            StatusMessage = $"Found {results.Count} model(s) for \"{query}\".";
            HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog search failed");
            ErrorMessage = $"Search failed: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadCatalogModelAsync(CatalogModelItem? item)
    {
        if (item is null || item.IsDownloading || item.IsDownloaded)
        {
            return;
        }

        ClearMessages();
        item.IsDownloading = true;
        item.Progress = 0;
        try
        {
            await foreach (var progress in _models.DownloadGgufAsync(item.Info))
            {
                PostToUi(() =>
                {
                    item.Progress = progress.Percent;
                    item.ProgressText = progress.TotalBytes > 0
                        ? $"{progress.Percent}% - {InstalledModelInfo.FormatSize(progress.BytesReceived)} / {InstalledModelInfo.FormatSize(progress.TotalBytes)}"
                        : $"{progress.Percent}%";
                });
            }

            item.IsDownloaded = true;
            item.IsDownloading = false;
            StatusMessage = $"Downloaded {item.Info.Name} ({item.LocalFileName}).";
            HasStatusMessage = true;
        }
        catch (OperationCanceledException)
        {
            item.IsDownloading = false;
            StatusMessage = $"Download of {item.Info.Name} was cancelled.";
            HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            item.IsDownloading = false;
            _logger.LogError(ex, "Failed to download {Model}", item.Info.Id);
            ErrorMessage = $"Error downloading {item.Info.Name}: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private void CancelDownload(CatalogModelItem? item)
    {
        if (item is not null)
        {
            _models.CancelDownload(item.Info.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteGgufAsync(CatalogModelItem? item)
    {
        if (item is null || item.LocalFileName.Length == 0)
        {
            return;
        }

        try
        {
            await _models.DeleteLocalGgufFileAsync(item.LocalFileName);
            item.IsDownloaded = false;
            StatusMessage = $"Deleted {item.LocalFileName}.";
            HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting file: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task ImportGgufAsync(CatalogModelItem? item)
    {
        if (item is null || !item.IsDownloaded)
        {
            return;
        }

        var modelName = item.Info.Name.ToLowerInvariant().Replace(' ', '-');
        ClearMessages();
        try
        {
            await foreach (var _ in _models.ImportGgufToOllamaAsync(item.LocalFileName, modelName))
            {
                // progress is coarse ("creating"/"success"); refresh when done
            }

            StatusMessage = $"Imported {item.LocalFileName} into Ollama as {modelName}.";
            HasStatusMessage = true;
            await RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import GGUF {File}", item.LocalFileName);
            ErrorMessage = $"Error importing into Ollama: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task DownloadWhisperAsync(WhisperModelItem? item)
    {
        if (item is null || item.IsDownloading || item.IsDownloaded)
        {
            return;
        }

        ClearMessages();
        item.IsDownloading = true;
        item.Progress = 0;
        try
        {
            await foreach (var progress in _models.DownloadWhisperModelAsync(item.Id))
            {
                PostToUi(() => item.Progress = progress.Percent);
            }

            item.IsDownloaded = true;
            item.IsDownloading = false;
            StatusMessage = $"Downloaded speech model {item.FileName}.";
            HasStatusMessage = true;
            await RefreshModelsAsync();
        }
        catch (OperationCanceledException)
        {
            item.IsDownloading = false;
            StatusMessage = $"Download of {item.FileName} was cancelled.";
            HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            item.IsDownloading = false;
            _logger.LogError(ex, "Failed to download speech model {Model}", item.Id);
            ErrorMessage = $"Error downloading {item.FileName}: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task DeleteWhisperAsync(WhisperModelItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _models.DeleteWhisperModelAsync(item.FileName);
            item.IsDownloaded = false;
            StatusMessage = $"Deleted {item.FileName}.";
            HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting file: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task SelectWhisperAsync(WhisperModelItem? item)
    {
        if (item is null || !item.IsDownloaded)
        {
            return;
        }

        try
        {
            var settings = await _store.GetSettingsAsync();
            settings.SpeechModelFile = item.FileName;
            await _store.SaveSettingsAsync(settings);
            StatusMessage = $"Speech model set to {item.FileName}.";
            HasStatusMessage = true;
            await RefreshModelsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error selecting speech model: {ex.Message}";
            HasError = true;
        }
    }

    private void ClearMessages()
    {
        HasError = false;
        HasStatusMessage = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private static string ExpectedGgufFileName(CatalogModelInfo info)
    {
        var file = Path.GetFileName(new Uri(info.DownloadUrl).LocalPath);
        return $"{info.RepoId.Replace('/', '-')}-{file}";
    }

    private static void PostToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
