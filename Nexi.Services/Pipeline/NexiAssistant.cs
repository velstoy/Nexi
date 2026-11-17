using Microsoft.Extensions.Logging;

using Nexi.Data;
using Nexi.Data.Models;
using Nexi.Services.AI;
using Nexi.Services.Commands;

namespace Nexi.Services.Pipeline;

/// <summary>
/// The command pipeline: persist input, ask the configured AI provider, parse any desktop
/// command out of the reply, classify its safety, execute it (or park it for confirmation),
/// and persist the outcome.
/// </summary>
public sealed class NexiAssistant : INexiAssistant
{
    private const int ConversationWindow = 20;

    private readonly INexiStore _store;
    private readonly IAssistantProviderFactory _providerFactory;
    private readonly IDesktopCommandExecutor _executor;
    private readonly ILogger<NexiAssistant> _logger;
    private readonly object _pendingLock = new();

    public NexiAssistant(
        INexiStore store,
        IAssistantProviderFactory providerFactory,
        IDesktopCommandExecutor executor,
        ILogger<NexiAssistant> logger)
    {
        _store = store;
        _providerFactory = providerFactory;
        _executor = executor;
        _logger = logger;
    }

    public DesktopCommand? PendingCommand
    {
        get { lock (_pendingLock) { return _pending; } }
    }

    private DesktopCommand? _pending;

    public async Task<AssistantResult> ProcessInputAsync(string input, CancellationToken ct = default)
    {
        input = input.Trim();
        if (input.Length == 0)
        {
            throw new ArgumentException("Input must not be empty.", nameof(input));
        }

        var settings = await _store.GetSettingsAsync(ct);
        var history = await _store.GetRecentMessagesAsync(ConversationWindow, ct);

        var conversation = new List<ChatMessage>(history)
        {
            new() { Role = ChatRole.User, Content = input }
        };

        var provider = _providerFactory.Create(settings);

        AssistantReply reply;
        try
        {
            reply = await provider.ReplyAsync(conversation, settings, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider {Provider} failed", provider.Name);
            reply = new AssistantReply
            {
                Text = $"The {provider.Name} provider failed: {ex.Message}\n" +
                       "Check the model/endpoint in the settings, or switch to the built-in local assistant.",
                ProviderName = provider.Name
            };
        }

        var text = reply.Text;
        var command = reply.Command ?? DesktopCommandParser.TryParse(reply.Text);
        CommandExecutionResult? execution = null;

        if (command is { Kind: not CommandKind.None })
        {
            command.Safety = CommandSafetyClassifier.Classify(command);

            switch (command.Safety)
            {
                case CommandSafety.Blocked:
                    text += "\n\nI can't do that - the command was blocked by the safety policy.";
                    _logger.LogWarning("Blocked command from {Provider}: {Kind} {Argument}", provider.Name, command.Kind, command.Argument);
                    break;

                case CommandSafety.Safe when settings.AutoConfirmSafeCommands:
                    execution = await _executor.ExecuteAsync(command, confirmed: true, ct);
                    text += "\n\n" + DescribeExecution(execution);
                    break;

                case CommandSafety.NeedsConfirmation:
                    lock (_pendingLock) { _pending = command; }
                    text += $"\n\nThis needs your confirmation before I run it: {command.Explanation ?? command.Argument}";
                    break;
            }
        }

        var userMessage = conversation[^1];
        await _store.AddMessageAsync(userMessage, ct);

        var assistantMessage = new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = text,
            Provider = reply.ProviderName,
            CommandKind = command?.Kind,
            CommandExecuted = execution?.Executed
        };
        await _store.AddMessageAsync(assistantMessage, ct);

        await _store.AddHistoryAsync(new CommandHistoryEntry
        {
            Input = input,
            AiReply = text,
            CommandKind = command?.Kind ?? CommandKind.None,
            Executed = execution?.Executed ?? false,
            Success = execution?.Success ?? false,
            Error = execution?.Error
        }, ct);

        await _store.TrimMessagesAsync(settings.HistoryLimit, ct);

        return new AssistantResult
        {
            Reply = text,
            Command = command,
            Execution = execution,
            ProviderName = reply.ProviderName
        };
    }

    public async Task<AssistantResult> ConfirmPendingAsync(CancellationToken ct = default)
    {
        DesktopCommand? pending;
        lock (_pendingLock)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending is null)
        {
            return new AssistantResult { Reply = "There is no pending command to confirm." };
        }

        var settings = await _store.GetSettingsAsync(ct);
        var execution = await _executor.ExecuteAsync(pending, confirmed: true, ct);
        var text = DescribeExecution(execution);

        await _store.AddMessageAsync(new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = text,
            CommandKind = pending.Kind,
            CommandExecuted = execution.Executed
        }, ct);

        await _store.AddHistoryAsync(new CommandHistoryEntry
        {
            Input = pending.Explanation ?? pending.Argument,
            AiReply = text,
            CommandKind = pending.Kind,
            Executed = execution.Executed,
            Success = execution.Success,
            Error = execution.Error
        }, ct);

        return new AssistantResult
        {
            Reply = text,
            Command = pending,
            Execution = execution
        };
    }

    public void DismissPending()
    {
        lock (_pendingLock) { _pending = null; }
    }

    private static string DescribeExecution(CommandExecutionResult execution) => execution switch
    {
        { Executed: true, Success: true } => $"Done. {execution.Output}",
        { Executed: true, Success: false } => $"I tried, but it failed: {execution.Error}",
        _ => $"Not executed: {execution.Error}"
    };
}
