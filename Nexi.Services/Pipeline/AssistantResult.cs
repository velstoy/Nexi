using Nexi.Data.Models;
using Nexi.Services.Commands;

namespace Nexi.Services.Pipeline;

/// <summary>The outcome of processing one user input through the whole pipeline.</summary>
public sealed class AssistantResult
{
    public required string Reply { get; init; }

    public DesktopCommand? Command { get; init; }

    public CommandExecutionResult? Execution { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    /// <summary>True when a command is waiting for the user to confirm it.</summary>
    public bool RequiresConfirmation =>
        Command is { Safety: CommandSafety.NeedsConfirmation } && (Execution is null || !Execution.Executed);
}
