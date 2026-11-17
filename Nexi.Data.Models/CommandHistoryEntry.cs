namespace Nexi.Data.Models;

/// <summary>A record of one processed user request and its outcome.</summary>
public sealed class CommandHistoryEntry
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Input { get; set; } = string.Empty;

    public string AiReply { get; set; } = string.Empty;

    public CommandKind CommandKind { get; set; } = CommandKind.None;

    public bool Executed { get; set; }

    public bool Success { get; set; }

    public string? Error { get; set; }
}
