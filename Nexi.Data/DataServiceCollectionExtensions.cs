using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexi.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite-backed data layer. The database file lives at
    /// <paramref name="databasePath"/>; when null, a per-user AppData location is used.
    /// </summary>
    public static IServiceCollection AddNexiData(this IServiceCollection services, string? databasePath = null)
    {
        var path = databasePath ?? DefaultDatabasePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContextFactory<NexiDbContext>(options =>
            options.UseSqlite($"Data Source={path}"));
        services.AddSingleton<INexiStore, NexiStore>();
        return services;
    }

    public static string DefaultDatabasePath() =>
        Environment.GetEnvironmentVariable("NEXI_DB_PATH") is { Length: > 0 } overridePath
            ? overridePath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nexi",
                "nexi.db");
}
