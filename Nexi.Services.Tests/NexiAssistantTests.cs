using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Data.Models;
using Nexi.Services.AI;
using Nexi.Services.Commands;
using Nexi.Services.Pipeline;

namespace Nexi.Services.Tests;

public class NexiAssistantTests
{
    private readonly FakeAssistantProvider _provider = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly NexiAssistant _assistant;
    private readonly Nexi.Data.INexiStore _store;

    public NexiAssistantTests()
    {
        _store = TestStore.Create();
        var factory = new FakeAssistantProviderFactory { Provider = _provider };
        var executor = new DesktopExecutor(_runner, NullLogger<DesktopExecutor>.Instance);
        _assistant = new NexiAssistant(_store, factory, executor, NullLogger<NexiAssistant>.Instance);
    }

    [Fact]
    public async Task Safe_command_is_executed_and_persisted()
    {
        _provider.Fallback = new AssistantReply
        {
            Text = "Opening it.",
            Command = new DesktopCommand { Kind = CommandKind.OpenUrl, Argument = "https://example.com" },
            ProviderName = "Fake provider"
        };

        var result = await _assistant.ProcessInputAsync("open example.com");

        Assert.True(result.Execution!.Success);
        Assert.Single(_runner.Started);
        Assert.Equal("https://example.com", _runner.Started[0].FileName);

        var messages = await _store.GetRecentMessagesAsync(10);
        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.User, messages[0].Role);
        Assert.Equal(ChatRole.Assistant, messages[1].Role);
        Assert.Equal(CommandKind.OpenUrl, messages[1].CommandKind);

        var history = await _store.GetRecentHistoryAsync(10);
        var entry = Assert.Single(history);
        Assert.True(entry.Executed);
        Assert.True(entry.Success);
        Assert.Equal(CommandKind.OpenUrl, entry.CommandKind);
    }

    [Fact]
    public async Task Confirmation_command_is_parked_then_run_on_confirm()
    {
        _provider.Fallback = new AssistantReply
        {
            Text = "I can run that.",
            Command = new DesktopCommand { Kind = CommandKind.RunScript, Argument = "echo hi" },
            ProviderName = "Fake provider"
        };

        var result = await _assistant.ProcessInputAsync("run echo hi");

        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(_assistant.PendingCommand);
        Assert.Empty(_runner.Started);
        Assert.Contains("confirmation", result.Reply);

        var confirmed = await _assistant.ConfirmPendingAsync();

        Assert.True(confirmed.Execution!.Success);
        Assert.Null(_assistant.PendingCommand);
        var launched = Assert.Single(_runner.Started);
        Assert.Contains("echo hi", launched.Arguments);
    }

    [Fact]
    public async Task Blocked_command_is_never_executed()
    {
        _provider.Fallback = new AssistantReply
        {
            Text = "Sure!",
            Command = new DesktopCommand { Kind = CommandKind.RunScript, Argument = "rm -rf /" },
            ProviderName = "Fake provider"
        };

        var result = await _assistant.ProcessInputAsync("run rm -rf /");

        Assert.False(result.RequiresConfirmation);
        Assert.Null(result.Execution);
        Assert.Empty(_runner.Started);
        Assert.Contains("blocked", result.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_failure_is_reported_not_thrown()
    {
        var failing = new ThrowingProvider();
        var assistant = new NexiAssistant(
            _store,
            new FakeAssistantProviderFactory { Provider = failing },
            new DesktopExecutor(_runner, NullLogger<DesktopExecutor>.Instance),
            NullLogger<NexiAssistant>.Instance);

        var result = await assistant.ProcessInputAsync("hello");

        Assert.Contains("failed", result.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Command_can_be_parsed_from_json_in_reply_text()
    {
        _provider.Fallback = new AssistantReply
        {
            Text = "Opening it for you.\n```json\n{\"kind\":\"OpenUrl\",\"argument\":\"https://example.org\"}\n```",
            ProviderName = "Fake provider"
        };

        var result = await _assistant.ProcessInputAsync("open example.org");

        Assert.NotNull(result.Command);
        Assert.Equal(CommandKind.OpenUrl, result.Command!.Kind);
        Assert.True(result.Execution!.Success);
    }

    [Fact]
    public async Task Plain_chat_reply_has_no_command_and_runs_nothing()
    {
        _provider.Fallback = new AssistantReply { Text = "Hi! How can I help?", ProviderName = "Fake provider" };

        var result = await _assistant.ProcessInputAsync("hello");

        Assert.Null(result.Command);
        Assert.Null(result.Execution);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public async Task Empty_input_throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _assistant.ProcessInputAsync("   "));
    }

    [Fact]
    public async Task Confirm_without_pending_is_a_no_op()
    {
        var result = await _assistant.ConfirmPendingAsync();
        Assert.Contains("no pending command", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public async Task DismissPending_clears_the_pending_command()
    {
        _provider.Fallback = new AssistantReply
        {
            Text = "ok",
            Command = new DesktopCommand { Kind = CommandKind.RunScript, Argument = "echo hi" }
        };

        await _assistant.ProcessInputAsync("run echo hi");
        Assert.NotNull(_assistant.PendingCommand);

        _assistant.DismissPending();

        Assert.Null(_assistant.PendingCommand);
    }

    private sealed class ThrowingProvider : IAssistantProvider
    {
        public string Name => "Throwing provider";

        public Task<AssistantReply> ReplyAsync(
            IReadOnlyList<ChatMessage> conversation,
            AppSettings settings,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("endpoint unreachable");
    }
}
