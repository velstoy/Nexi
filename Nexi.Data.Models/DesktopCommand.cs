namespace Nexi.Data.Models;

/// <summary>
/// A structured desktop action that Nexi can execute on behalf of the user.
/// Produced either by an AI provider (parsed from its JSON output) or by the
/// built-in local assistant's intent matcher.
/// </summary>
public sealed class DesktopCommand
{
    public CommandKind Kind { get; set; } = CommandKind.None;

    public string Argument { get; set; } = string.Empty;

    /// <summary>Raw JSON emitted by the AI model, when the command came from an LLM.</summary>
    public string? RawJson { get; set; }

    public CommandSafety Safety { get; set; } = CommandSafety.NeedsConfirmation;

    /// <summary>Short human-readable explanation of what the command will do.</summary>
    public string? Explanation { get; set; }
}
