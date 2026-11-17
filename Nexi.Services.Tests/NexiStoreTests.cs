using Nexi.Data.Models;

namespace Nexi.Services.Tests;

public class NexiStoreTests
{
    [Fact]
    public async Task Settings_roundtrip()
    {
        var store = TestStore.Create();

        var settings = await store.GetSettingsAsync();
        Assert.Equal(AiProviderKind.LocalAssistant, settings.Provider);

        settings.Provider = AiProviderKind.Ollama;
        settings.Model = "mistral";
        settings.ApiKey = "secret";
        await store.SaveSettingsAsync(settings);

        var reloaded = await store.GetSettingsAsync();
        Assert.Equal(AiProviderKind.Ollama, reloaded.Provider);
        Assert.Equal("mistral", reloaded.Model);
        Assert.Equal("secret", reloaded.ApiKey);
    }

    [Fact]
    public async Task Messages_are_returned_in_order()
    {
        var store = TestStore.Create();
        await store.AddMessageAsync(new ChatMessage { Role = ChatRole.User, Content = "first", Timestamp = DateTime.UtcNow.AddMinutes(-2) });
        await store.AddMessageAsync(new ChatMessage { Role = ChatRole.Assistant, Content = "second", Timestamp = DateTime.UtcNow.AddMinutes(-1) });

        var messages = await store.GetRecentMessagesAsync(10);

        Assert.Equal(2, messages.Count);
        Assert.Equal("first", messages[0].Content);
        Assert.Equal("second", messages[1].Content);
    }

    [Fact]
    public async Task TrimMessages_keeps_only_newest()
    {
        var store = TestStore.Create();
        for (var i = 0; i < 10; i++)
        {
            await store.AddMessageAsync(new ChatMessage
            {
                Role = ChatRole.User,
                Content = $"msg {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            });
        }

        await store.TrimMessagesAsync(3);

        var messages = await store.GetRecentMessagesAsync(100);
        Assert.Equal(3, messages.Count);
        Assert.Equal("msg 7", messages[0].Content);
        Assert.Equal("msg 9", messages[2].Content);
    }

    [Fact]
    public async Task History_entries_are_stored_and_ordered()
    {
        var store = TestStore.Create();
        await store.AddHistoryAsync(new CommandHistoryEntry { Input = "a", Timestamp = DateTime.UtcNow.AddMinutes(-1) });
        await store.AddHistoryAsync(new CommandHistoryEntry { Input = "b", Timestamp = DateTime.UtcNow });

        var history = await store.GetRecentHistoryAsync(10);

        Assert.Equal(2, history.Count);
        Assert.Equal("a", history[0].Input);
        Assert.Equal("b", history[1].Input);
    }
}
