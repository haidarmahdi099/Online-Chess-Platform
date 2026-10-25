using Microsoft.EntityFrameworkCore;
using Rating.Domain.Entities;

namespace Rating.Infrastructure.Persistence;

public sealed class RatingDbContext(
    DbContextOptions<RatingDbContext> options) : DbContext(options)
{
    public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();

    public DbSet<RatingHistory> RatingHistories => Set<RatingHistory>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayerRating>(entity =>
        {
            entity.ToTable("player_ratings");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Version).IsRowVersion();

            entity.Property(x => x.Category)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Rating).IsRequired();

            entity.HasIndex(x => new { x.PlayerId, x.Category })
                .IsUnique();
        });

        modelBuilder.Entity<RatingHistory>(entity =>
        {
            entity.ToTable("rating_histories");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Category)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Reason)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => new { x.EventId, x.PlayerId, x.Category })
                .IsUnique();
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("processed_events");
            entity.HasKey(x => x.EventId);

            entity.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();
        });
    }
}
