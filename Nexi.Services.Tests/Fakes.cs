using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Nexi.Data;
using Nexi.Data.Models;
using Nexi.Services.AI;
using Nexi.Services.Commands;
using Nexi.Services.Pipeline;

namespace Nexi.Services.Tests;

public sealed class FakeAssistantProvider : IAssistantProvider
{
    public string Name => "Fake provider";

    public Queue<AssistantReply> Replies { get; } = new();

    public AssistantReply Fallback { get; set; } = new() { Text = "ok" };

    public IReadOnlyList<ChatMessage>? LastConversation { get; private set; }

    public Task<AssistantReply> ReplyAsync(
        IReadOnlyList<ChatMessage> conversation,
        AppSettings settings,
        CancellationToken ct = default)
    {
        LastConversation = conversation;
        return Task.FromResult(Replies.Count > 0 ? Replies.Dequeue() : Fallback);
    }
}

public sealed class FakeAssistantProviderFactory : IAssistantProviderFactory
{
    public IAssistantProvider Provider { get; set; } = new FakeAssistantProvider();

    public IAssistantProvider Create(AppSettings settings) => Provider;
}

public sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, string Arguments)> Started { get; } = new();

    public void Start(string fileName, string arguments, bool useShellExecute = true) =>
        Started.Add((fileName, arguments));
}

public static class TestStore
{
    public static INexiStore Create()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<NexiDbContext>(options =>
            options.UseInMemoryDatabase("nexi-tests-" + Guid.NewGuid()));
        services.AddSingleton<INexiStore, NexiStore>();
        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<INexiStore>();
        store.EnsureCreatedAsync().GetAwaiter().GetResult();
        return store;
    }
}
