using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

using Microsoft.Extensions.DependencyInjection;

using Nexi.Data;
using Nexi.Services;
using Nexi.Services.Models;
using Nexi.UI.Services;
using Nexi.UI.ViewModels;
using Nexi.UI.Views;

namespace Nexi.UI;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddNexiServices();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<ChatHistoryViewModel>();
        services.AddTransient<ModelsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton(sp => new MainViewModel(
            sp.GetRequiredService<ChatViewModel>(),
            sp.GetRequiredService<ChatHistoryViewModel>(),
            sp.GetRequiredService<ModelsViewModel>(),
            sp.GetRequiredService<SettingsViewModel>(),
            () => sp.GetRequiredService<ChatViewModel>()));
        Services = services.BuildServiceProvider();

        var store = Services.GetRequiredService<INexiStore>();
        store.EnsureCreatedAsync().GetAwaiter().GetResult();
        var settings = store.GetSettingsAsync().GetAwaiter().GetResult();
        ApplyTheme(settings.Theme);

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        var models = Services.GetRequiredService<ModelsViewModel>();
        var history = Services.GetRequiredService<ChatHistoryViewModel>();
        var settingsVm = Services.GetRequiredService<SettingsViewModel>();
        settingsVm.Load();
        settingsVm.SetSpeechModels(Services.GetRequiredService<IModelManager>().WhisperModelOptions);
        history.Load();
        models.Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            desktop.ShutdownRequested += (_, _) => (Services as IDisposable)?.Dispose();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string? theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        void Apply() => Application.Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }
}
