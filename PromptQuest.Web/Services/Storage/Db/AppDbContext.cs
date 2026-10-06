using global::PromptQuest.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace PromptQuest.Web.Services.Storage.Db;

public sealed class AppDbContext : DbContext
{
    /// <summary>Initializes the game data context with the supplied database options.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<LevelProgress> LevelProgresses => Set<LevelProgress>();
    public DbSet<Attempt> Attempts => Set<Attempt>();

    /// <summary>
    /// Configures application-assigned keys, required fields, length limits, and indexes,
    /// including unique progress per player and level and cascading deletion of a player's progress and attempts.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nickname).IsRequired().HasMaxLength(100);

            e.HasMany(x => x.Progress)
             .WithOne()
             .HasForeignKey(x => x.PlayerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LevelProgress>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.LevelId).IsRequired().HasMaxLength(64);

            e.HasIndex(x => new { x.PlayerId, x.LevelId }).IsUnique();
            e.HasIndex(x => new { x.LevelId, x.Completed, x.BestScore });
            e.HasIndex(x => new { x.LevelId, x.Completed, x.BestPromptLength });
        });

        b.Entity<Attempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.LevelId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Prompt).IsRequired();
            e.Property(x => x.Code).IsRequired();

            e.HasOne<Player>()
             .WithMany()
             .HasForeignKey(x => x.PlayerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.PlayerId, x.LevelId });
        });
    }
}
