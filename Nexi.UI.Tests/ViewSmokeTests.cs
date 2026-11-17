using System.Runtime.CompilerServices;

using Avalonia.Controls;
using Avalonia.Headless;

using Microsoft.Extensions.Logging.Abstractions;

using Nexi.UI.ViewModels;
using Nexi.UI.Views;

namespace Nexi.UI.Tests;

/// <summary>
/// Boots the app once on a dedicated headless UI thread (Avalonia objects are
/// thread-affine) and lets tests marshal work onto that thread via <see cref="RunAsync"/>.
/// </summary>
public static class HeadlessSetup
{
    private static HeadlessUnitTestSession? _session;

    [ModuleInitializer]
    public static void Initialize()
    {
        // Isolate the headless app from the real per-user database.
        Environment.SetEnvironmentVariable(
            "NEXI_DB_PATH",
            Path.Combine(Path.GetTempPath(), "nexi-headless-" + Guid.NewGuid().ToString("N"), "nexi.db"));

        _session = HeadlessUnitTestSession.StartNew(typeof(App));
    }

    /// <summary>Runs <paramref name="body"/> on the UI thread and waits for it to finish.</summary>
    public static async Task RunAsync(Func<Task> body) =>
        await await _session!.Dispatch(body, CancellationToken.None);

    public static Task RunAsync(Action body) =>
        _session!.Dispatch(body, CancellationToken.None);
}

public class ViewSmokeTests
{
    private static MainViewModel CreateMainViewModel()
    {
        var assistant = new FakeAssistant();
        ChatViewModel NewChat() => new(assistant, new UnavailableSpeech(), new FakeClipboard(), NullLogger<ChatViewModel>.Instance);
        return new MainViewModel(
            NewChat(),
            new ChatHistoryViewModel(TestStore.Create(), NullLogger<ChatHistoryViewModel>.Instance),
            new ModelsViewModel(new FakeModelManager(), TestStore.Create(), NullLogger<ModelsViewModel>.Instance),
            new SettingsViewModel(TestStore.Create(), new UnavailableSpeech(), NullLogger<SettingsViewModel>.Instance),
            NewChat);
    }

    [Fact]
    public Task MainWindow_loads_shell_with_sidebar() => HeadlessSetup.RunAsync(() =>
    {
        var vm = CreateMainViewModel();
        var window = new MainWindow { DataContext = vm };

        var view = Assert.IsType<MainView>(window.Content);
        Assert.Same(vm, view.DataContext);
        Assert.Equal("Nexi", window.Title);
    });

    [Fact]
    public Task All_pages_render_headless() => HeadlessSetup.RunAsync(() =>
    {
        var vm = CreateMainViewModel();

        AssertPage<ChatViewModel, ChatView>(vm, vm.NewChatCommand);
        AssertPage<ChatHistoryViewModel, ChatHistoryView>(vm, vm.ChatHistoryCommand);
        AssertPage<ModelsViewModel, ModelsView>(vm, vm.ModelsCommand);
        AssertPage<SettingsViewModel, SettingsView>(vm, vm.SettingsCommand);
    });

    private static void AssertPage<TPage, TView>(MainViewModel vm, CommunityToolkit.Mvvm.Input.IRelayCommand command)
        where TPage : ViewModelBase
        where TView : UserControl, new()
    {
        command.Execute(null);
        var page = Assert.IsType<TPage>(vm.CurrentPage);
        var view = new TView { DataContext = page };
        var window = new Window { Content = view, Width = 900, Height = 600 };
        window.Show();
        Assert.True(view.IsVisible);
        window.Close();
    }
}
