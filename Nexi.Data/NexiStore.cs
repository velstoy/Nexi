using Microsoft.EntityFrameworkCore;

using Nexi.Data.Models;

namespace Nexi.Data;

public sealed class NexiStore : INexiStore
{
    private readonly IDbContextFactory<NexiDbContext> _contextFactory;

    public NexiStore(IDbContextFactory<NexiDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await context.Database.EnsureCreatedAsync(ct);
        if (context.Database.IsRelational())
        {
            await AddMissingColumnsAsync(context, ct);
        }
    }

    /// <summary>
    /// EnsureCreated only creates tables when the database file does not exist yet.
    /// When the app is updated and columns were added to an existing table, add them
    /// in place (SQLite supports ADD COLUMN) so an existing database keeps working.
    /// </summary>
    private static async Task AddMissingColumnsAsync(NexiDbContext context, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table is null)
                {
                    continue;
                }

                var storeObject = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entityType.GetSchema());
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var pragma = connection.CreateCommand())
                {
                    pragma.CommandText = $"PRAGMA table_info(\"{table}\")";
                    await using var reader = await pragma.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        existing.Add(reader.GetString(1));
                    }
                }

                if (existing.Count == 0)
                {
                    continue; // Table does not exist yet; nothing to upgrade.
                }

                var requiredDefaults = new List<(string Column, string DefaultSql)>();
                foreach (var property in entityType.GetProperties())
                {
                    var column = property.GetColumnName(storeObject);
                    if (column is null)
                    {
                        continue;
                    }

                    var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    var sqlType = clr == typeof(string) || clr == typeof(DateTime) ? "TEXT"
                        : clr.IsEnum || clr == typeof(bool) || clr == typeof(byte) || clr == typeof(short)
                            || clr == typeof(int) || clr == typeof(long) ? "INTEGER"
                        : "REAL";
                    var required = !property.IsNullable;
                    var defaultValue = property.GetDefaultValue()
                        ?? (clr.IsValueType ? Activator.CreateInstance(clr) : null);

                    if (required)
                    {
                        requiredDefaults.Add((column, FormatDefault(defaultValue, sqlType)));
                    }

                    if (existing.Contains(column))
                    {
                        continue;
                    }

                    var defaultSql = required ? $" DEFAULT {FormatDefault(defaultValue, sqlType)}" : string.Empty;
                    await using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType}{(required ? " NOT NULL" : string.Empty)}{defaultSql}";
                    await alter.ExecuteNonQueryAsync(ct);
                }

                // Backfill NULLs in required columns (rows that predate the column or an
                // older nullable definition of it).
                foreach (var (column, defaultSql) in requiredDefaults)
                {
                    if (!existing.Contains(column))
                    {
                        continue; // Just added with a DEFAULT; nothing to backfill.
                    }

                    await using var update = connection.CreateCommand();
                    update.CommandText = $"UPDATE \"{table}\" SET \"{column}\" = {defaultSql} WHERE \"{column}\" IS NULL";
                    await update.ExecuteNonQueryAsync(ct);
                }
            }
        }
        finally
        {
            if (!wasOpen)
            {
                connection.Close();
            }
        }
    }

    private static string FormatDefault(object? value, string sqlType) => value switch
    {
        null => sqlType == "TEXT" ? "''" : "0",
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b ? "1" : "0",
        Enum e => Convert.ToInt64(e).ToString(System.Globalization.CultureInfo.InvariantCulture),
        DateTime dt => $"'{dt:O}'",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => "0"
    };

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var settings = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        return settings ?? new AppSettings { Id = 1 };
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        settings.Id = 1;
        context.Settings.Update(settings);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(int count, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.ChatMessages
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(ct);
    }

    public async Task AddHistoryAsync(CommandHistoryEntry entry, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.CommandHistory.Add(entry);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CommandHistoryEntry>> GetRecentHistoryAsync(int count, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.CommandHistory
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct);
    }

    public async Task TrimMessagesAsync(int keep, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var total = await context.ChatMessages.CountAsync(ct);
        if (total <= keep)
        {
            return;
        }

        var toRemove = await context.ChatMessages
            .OrderByDescending(m => m.Timestamp)
            .Skip(keep)
            .ToListAsync(ct);

        context.ChatMessages.RemoveRange(toRemove);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteHistoryEntryAsync(long id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entry = await context.CommandHistory.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is not null)
        {
            context.CommandHistory.Remove(entry);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task ClearHistoryAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var all = await context.CommandHistory.ToListAsync(ct);
        context.CommandHistory.RemoveRange(all);
        await context.SaveChangesAsync(ct);
    }
}
