namespace Nexi.Data.Models;

/// <summary>A single message in the conversation with Nexi.</summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ChatRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Name of the AI provider that produced this message, if assistant.</summary>
    public string? Provider { get; set; }

    /// <summary>The desktop command attached to this message, if any.</summary>
    public CommandKind? CommandKind { get; set; }

    public bool? CommandExecuted { get; set; }
}
