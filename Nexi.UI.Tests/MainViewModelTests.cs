using Microsoft.Extensions.Logging.Abstractions;

using Nexi.UI.ViewModels;

namespace Nexi.UI.Tests;

public class MainViewModelTests
{
    private static (MainViewModel vm, ChatViewModel chat, ChatHistoryViewModel history, ModelsViewModel models, SettingsViewModel settings) Create()
    {
        var assistant = new FakeAssistant();
        var chat = new ChatViewModel(assistant, new UnavailableSpeech(), new FakeClipboard(), NullLogger<ChatViewModel>.Instance);
        var history = new ChatHistoryViewModel(TestStore.Create(), NullLogger<ChatHistoryViewModel>.Instance);
        var models = new ModelsViewModel(new FakeModelManager(), TestStore.Create(), NullLogger<ModelsViewModel>.Instance);
        var settings = new SettingsViewModel(TestStore.Create(), new UnavailableSpeech(), NullLogger<SettingsViewModel>.Instance);
        var vm = new MainViewModel(chat, history, models, settings, () => new ChatViewModel(assistant, new UnavailableSpeech(), new FakeClipboard(), NullLogger<ChatViewModel>.Instance));
        return (vm, chat, history, models, settings);
    }

    [Fact]
    public void Starts_on_chat_page_with_expanded_sidebar()
    {
        var (vm, chat, _, _, _) = Create();

        Assert.Same(chat, vm.CurrentPage);
        Assert.True(vm.IsSidebarExpanded);
        Assert.Equal(250, vm.SidebarWidth);
    }

    [Fact]
    public void Sidebar_toggles_between_widths()
    {
        var (vm, _, _, _, _) = Create();

        vm.ToggleSidebarCommand.Execute(null);

        Assert.False(vm.IsSidebarExpanded);
        Assert.Equal(60, vm.SidebarWidth);
    }

    [Fact]
    public void Navigation_switches_pages()
    {
        var (vm, _, history, models, settings) = Create();

        vm.ModelsCommand.Execute(null);
        Assert.Same(models, vm.CurrentPage);

        vm.SettingsCommand.Execute(null);
        Assert.Same(settings, vm.CurrentPage);

        vm.ChatHistoryCommand.Execute(null);
        Assert.Same(history, vm.CurrentPage);
    }

    [Fact]
    public void NewChat_creates_a_fresh_chat_page()
    {
        var (vm, chat, _, _, _) = Create();
        vm.ModelsCommand.Execute(null);

        vm.NewChatCommand.Execute(null);

        Assert.IsType<ChatViewModel>(vm.CurrentPage);
        Assert.NotSame(chat, vm.CurrentPage);
    }
}
