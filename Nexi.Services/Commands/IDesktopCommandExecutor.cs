using Nexi.Data.Models;

namespace Nexi.Services.Commands;

public interface IDesktopCommandExecutor
{
    /// <summary>
    /// Executes a command. Blocked commands are refused; NeedsConfirmation commands only run
    /// when <paramref name="confirmed"/> is true.
    /// </summary>
    Task<CommandExecutionResult> ExecuteAsync(DesktopCommand command, bool confirmed, CancellationToken ct = default);
}
