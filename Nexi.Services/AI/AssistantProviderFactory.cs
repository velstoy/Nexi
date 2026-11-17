using Microsoft.Extensions.DependencyInjection;

using Nexi.Data.Models;

namespace Nexi.Services.AI;

/// <summary>Resolves the keyed <see cref="IAssistantProvider"/> matching the configured provider kind.</summary>
public sealed class AssistantProviderFactory : IAssistantProviderFactory
{
    private readonly IServiceProvider _services;

    public AssistantProviderFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IAssistantProvider Create(AppSettings settings) => settings.Provider switch
    {
        AiProviderKind.LocalAssistant => _services.GetRequiredKeyedService<IAssistantProvider>(AiProviderKind.LocalAssistant),
        AiProviderKind.Ollama => _services.GetRequiredKeyedService<IAssistantProvider>(AiProviderKind.Ollama),
        AiProviderKind.OpenAICompatible => _services.GetRequiredKeyedService<IAssistantProvider>(AiProviderKind.OpenAICompatible),
        _ => throw new ArgumentOutOfRangeException(nameof(settings), $"Unknown provider kind: {settings.Provider}")
    };
}
