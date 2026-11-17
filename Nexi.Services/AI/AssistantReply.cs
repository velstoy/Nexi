using Nexi.Data.Models;

namespace Nexi.Services.AI;

/// <summary>The output of an AI provider: a conversational reply plus an optional desktop command.</summary>
public sealed class AssistantReply
{
    public required string Text { get; init; }

    public DesktopCommand? Command { get; init; }

    public string ProviderName { get; init; } = string.Empty;
}
