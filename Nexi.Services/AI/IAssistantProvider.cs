using Nexi.Data.Models;

namespace Nexi.Services.AI;

/// <summary>
/// A source of AI replies. Implementations: built-in rule-based assistant (always available,
/// offline), Ollama (local models) and any OpenAI-compatible cloud endpoint.
/// </summary>
public interface IAssistantProvider
{
    string Name { get; }

    Task<AssistantReply> ReplyAsync(
        IReadOnlyList<ChatMessage> conversation,
        AppSettings settings,
        CancellationToken ct = default);
}
