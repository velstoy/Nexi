using Microsoft.EntityFrameworkCore;

using Nexi.Data.Models;

namespace Nexi.Data;

/// <summary>SQLite-backed context for chat history, command history and settings.</summary>
public sealed class NexiDbContext : DbContext
{
    public NexiDbContext(DbContextOptions<NexiDbContext> options) : base(options)
    {
    }

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<CommandHistoryEntry> CommandHistory => Set<CommandHistoryEntry>();

    public DbSet<AppSettings> Settings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.Timestamp).IsRequired();
            entity.HasIndex(m => m.Timestamp);
        });

        modelBuilder.Entity<CommandHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            // Defaults mirror the C# property initializers so that columns added later
            // (via in-place schema upgrades on existing databases) get sensible values.
            entity.Property(s => s.Provider).HasDefaultValue(AiProviderKind.LocalAssistant);
            entity.Property(s => s.Model).IsRequired().HasDefaultValue("llama3.2");
            entity.Property(s => s.BaseUrl).IsRequired().HasDefaultValue("http://localhost:11434");
            entity.Property(s => s.VoiceEnabled).HasDefaultValue(false);
            entity.Property(s => s.InputDeviceIndex).HasDefaultValue(-1);
            entity.Property(s => s.SpeechModelFile).IsRequired().HasDefaultValue("ggml-tiny.bin");
            entity.Property(s => s.Theme).IsRequired().HasDefaultValue("Dark");
            entity.Property(s => s.AutoConfirmSafeCommands).HasDefaultValue(true);
            entity.Property(s => s.HistoryLimit).HasDefaultValue(200);
            entity.HasData(new AppSettings { Id = 1 });
        });
    }
}
