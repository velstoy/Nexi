using Nexi.Data.Models;

namespace Nexi.Services.Pipeline;

/// <summary>The main entry point of the services layer, used by the UI.</summary>
public interface INexiAssistant
{
    /// <summary>A command classified as NeedsConfirmation that has not run yet, if any.</summary>
    DesktopCommand? PendingCommand { get; }

    Task<AssistantResult> ProcessInputAsync(string input, CancellationToken ct = default);

    /// <summary>Runs the pending command after the user confirmed it.</summary>
    Task<AssistantResult> ConfirmPendingAsync(CancellationToken ct = default);

    /// <summary>Drops the pending command without running it.</summary>
    void DismissPending();
}
