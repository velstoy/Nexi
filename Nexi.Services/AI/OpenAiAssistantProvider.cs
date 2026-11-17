using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Nexi.Data.Models;
using Nexi.Services.Commands;

using System.ClientModel;

using OpenAI;

using ChatMessage = Nexi.Data.Models.ChatMessage;

namespace Nexi.Services.AI;

/// <summary>Any OpenAI-compatible chat endpoint (OpenAI, Azure OpenAI, Groq, OpenRouter, ...).</summary>
public sealed class OpenAiAssistantProvider : IAssistantProvider
{
    private readonly ILogger<OpenAiAssistantProvider> _logger;

    public OpenAiAssistantProvider(ILogger<OpenAiAssistantProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "OpenAI-compatible (cloud)";

    public async Task<AssistantReply> ReplyAsync(
        IReadOnlyList<ChatMessage> conversation,
        AppSettings settings,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "An API key is required for cloud providers. Add one in the Nexi settings, " +
                "or switch to the local assistant / Ollama.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://api.openai.com/v1"
            : settings.BaseUrl.TrimEnd('/') + "/v1";

        var client = new OpenAIClient(
            new ApiKeyCredential(settings.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = client.GetChatClient(settings.Model).AsIChatClient();
        var messages = OllamaAssistantProvider.ToChatMessages(conversation);
        var options = new ChatOptions { ModelId = settings.Model, Temperature = 0.2f };

        _logger.LogInformation("Cloud request to {BaseUrl} with model {Model}", baseUrl, settings.Model);
        var response = await chatClient.GetResponseAsync(messages, options, ct);
        var text = response.Text;

        return new AssistantReply
        {
            Text = text,
            Command = DesktopCommandParser.TryParse(text),
            ProviderName = Name
        };
    }
}
