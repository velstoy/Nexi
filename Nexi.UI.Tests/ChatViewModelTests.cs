using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Data.Models;
using Nexi.Services.Commands;
using Nexi.Services.Pipeline;
using Nexi.UI.ViewModels;

namespace Nexi.UI.Tests;

public class ChatViewModelTests
{
    private static ChatViewModel Create(FakeAssistant assistant, FakeClipboard? clipboard = null) =>
        new(assistant, new UnavailableSpeech(), clipboard ?? new FakeClipboard(), NullLogger<ChatViewModel>.Instance);

    [Fact]
    public async Task Send_adds_user_and_assistant_messages_and_clears_input()
    {
        var assistant = new FakeAssistant();
        assistant.Results.Enqueue(new AssistantResult { Reply = "Hi!", ProviderName = "Fake" });
        var vm = Create(assistant);

        vm.CurrentMessage = "hello";
        await vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Messages.Count); // welcome + user + assistant
        Assert.True(vm.Messages[1].IsUser);
        Assert.Equal("hello", vm.Messages[1].Content);
        Assert.False(vm.Messages[2].IsUser);
        Assert.Equal("Hi!", vm.Messages[2].Content);
        Assert.Equal(string.Empty, vm.CurrentMessage);
        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public async Task Send_with_empty_input_does_nothing()
    {
        var vm = Create(new FakeAssistant());
        vm.CurrentMessage = "   ";

        await vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Single(vm.Messages); // only the welcome message
    }

    [Fact]
    public async Task Confirmation_command_shows_banner_and_confirm_runs_it()
    {
        var assistant = new FakeAssistant();
        assistant.Results.Enqueue(new AssistantResult
        {
            Reply = "Needs confirmation.",
            Command = new DesktopCommand
            {
                Kind = CommandKind.RunScript,
                Argument = "echo hi",
                Safety = CommandSafety.NeedsConfirmation,
                Explanation = "Run script: echo hi"
            }
        });
        var vm = Create(assistant);

        vm.CurrentMessage = "run echo hi";
        await vm.SendMessageCommand.ExecuteAsync(null);

        Assert.True(vm.ShowPendingConfirmation);
        Assert.Contains("echo hi", vm.PendingText);

        await vm.ConfirmPendingCommand.ExecuteAsync(null);

        Assert.Equal(1, assistant.ConfirmCalls);
        Assert.False(vm.ShowPendingConfirmation);
        Assert.Equal("ran it", vm.Messages[^1].Content);
    }

    [Fact]
    public async Task Decline_dismisses_pending_command()
    {
        var assistant = new FakeAssistant();
        assistant.Results.Enqueue(new AssistantResult
        {
            Reply = "Needs confirmation.",
            Command = new DesktopCommand
            {
                Kind = CommandKind.RunScript,
                Argument = "echo hi",
                Safety = CommandSafety.NeedsConfirmation
            }
        });
        var vm = Create(assistant);

        vm.CurrentMessage = "run echo hi";
        await vm.SendMessageCommand.ExecuteAsync(null);
        vm.DeclinePendingCommand.Execute(null);

        Assert.Equal(1, assistant.DismissCalls);
        Assert.False(vm.ShowPendingConfirmation);
    }

    [Fact]
    public async Task Voice_toggle_reports_unavailability()
    {
        var vm = Create(new FakeAssistant());

        await vm.ToggleVoiceCommand.ExecuteAsync(null);

        Assert.False(vm.IsVoiceModeEnabled);
        Assert.Equal("Voice input is not available on this machine.", vm.StatusText);
    }

    [Fact]
    public async Task Copy_to_clipboard_uses_clipboard_service()
    {
        var clipboard = new FakeClipboard();
        var vm = Create(new FakeAssistant(), clipboard);

        await vm.CopyToClipboardCommand.ExecuteAsync("some text");

        Assert.Equal("some text", clipboard.LastText);
    }

    [Fact]
    public void Command_instructions_toggle()
    {
        var vm = Create(new FakeAssistant());
        Assert.False(vm.ShowCommandInstructions);

        vm.ToggleCommandInstructionsCommand.Execute(true);
        Assert.True(vm.ShowCommandInstructions);

        vm.ToggleCommandInstructionsCommand.Execute(false);
        Assert.False(vm.ShowCommandInstructions);
    }

    [Fact]
    public void HasMessageText_tracks_input()
    {
        var vm = Create(new FakeAssistant());
        Assert.False(vm.HasMessageText);

        vm.CurrentMessage = "hi";
        Assert.True(vm.HasMessageText);
    }
}
