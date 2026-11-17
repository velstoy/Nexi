using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Nexi.Data.Models;
using Nexi.Services.Commands;

using OllamaSharp;

using ChatMessage = Nexi.Data.Models.ChatMessage;
using ChatRole = Nexi.Data.Models.ChatRole;

namespace Nexi.Services.AI;

/// <summary>Local models served by Ollama (http://localhost:11434 by default).</summary>
public sealed class OllamaAssistantProvider : IAssistantProvider
{
    private readonly ILogger<OllamaAssistantProvider> _logger;

    public OllamaAssistantProvider(ILogger<OllamaAssistantProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "Ollama (local)";

    public async Task<AssistantReply> ReplyAsync(
        IReadOnlyList<ChatMessage> conversation,
        AppSettings settings,
        CancellationToken ct = default)
    {
        IChatClient client = new OllamaApiClient(new Uri(settings.BaseUrl.TrimEnd('/') + "/"));
        var messages = ToChatMessages(conversation);
        var options = new ChatOptions { ModelId = settings.Model, Temperature = 0.2f };

        _logger.LogInformation("Ollama request to {BaseUrl} with model {Model}", settings.BaseUrl, settings.Model);
        var response = await client.GetResponseAsync(messages, options, ct);
        var text = response.Text;

        return new AssistantReply
        {
            Text = text,
            Command = DesktopCommandParser.TryParse(text),
            ProviderName = Name
        };
    }

    internal static List<Microsoft.Extensions.AI.ChatMessage> ToChatMessages(IReadOnlyList<ChatMessage> conversation)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.System, SystemPrompts.DesktopAgent)
        };

        foreach (var message in conversation)
        {
            var role = message.Role switch
            {
                ChatRole.User => Microsoft.Extensions.AI.ChatRole.User,
                ChatRole.Assistant => Microsoft.Extensions.AI.ChatRole.Assistant,
                _ => Microsoft.Extensions.AI.ChatRole.System
            };
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, message.Content));
        }

        return messages;
    }
}
