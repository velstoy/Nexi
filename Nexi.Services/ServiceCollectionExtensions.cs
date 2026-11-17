using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Nexi.Data;
using Nexi.Data.Models;
using Nexi.Services.AI;
using Nexi.Services.Commands;
using Nexi.Services.Models;
using Nexi.Services.Pipeline;
using Nexi.Services.Speech;

namespace Nexi.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the whole Nexi core: data layer, AI providers, command pipeline and speech.
    /// </summary>
    public static IServiceCollection AddNexiServices(this IServiceCollection services, string? databasePath = null)
    {
        services.AddNexiData(databasePath);

        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddDebug());

        services.AddKeyedSingleton<IAssistantProvider, LocalAssistantProvider>(AiProviderKind.LocalAssistant);
        services.AddKeyedSingleton<IAssistantProvider, OllamaAssistantProvider>(AiProviderKind.Ollama);
        services.AddKeyedSingleton<IAssistantProvider, OpenAiAssistantProvider>(AiProviderKind.OpenAICompatible);
        services.AddSingleton<IAssistantProviderFactory, AssistantProviderFactory>();

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IDesktopCommandExecutor, DesktopExecutor>();
        services.AddSingleton<INexiAssistant, NexiAssistant>();

        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(30) });
        services.AddSingleton<IHuggingFaceClient, HuggingFaceClient>();
        services.AddSingleton<IOllamaAdmin, OllamaAdmin>();
        services.AddSingleton<IModelManager, ModelManager>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ISpeechRecognitionService, WhisperSpeechRecognitionService>();
        }
        else
        {
            services.AddSingleton<ISpeechRecognitionService>(
                new NullSpeechRecognitionService { Status = "Voice input is only supported on Windows." });
        }

        return services;
    }

    /// <summary>Creates the database schema if needed. Call once at startup.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var store = services.GetRequiredService<INexiStore>();
        await store.EnsureCreatedAsync(ct);
    }
}
