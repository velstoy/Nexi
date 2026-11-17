using Nexi.Data.Models;

namespace Nexi.Data;

/// <summary>Persistence facade used by the services layer.</summary>
public interface INexiStore
{
    Task EnsureCreatedAsync(CancellationToken ct = default);

    Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);

    Task AddMessageAsync(ChatMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(int count, CancellationToken ct = default);

    Task AddHistoryAsync(CommandHistoryEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<CommandHistoryEntry>> GetRecentHistoryAsync(int count, CancellationToken ct = default);

    /// <summary>Keeps only the newest <paramref name="keep"/> chat messages.</summary>
    Task TrimMessagesAsync(int keep, CancellationToken ct = default);

    Task DeleteHistoryEntryAsync(long id, CancellationToken ct = default);

    Task ClearHistoryAsync(CancellationToken ct = default);
}
