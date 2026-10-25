using Microsoft.EntityFrameworkCore;
using Puzzle.Domain.Entities;
namespace Puzzle.Infrastructure.Persistence;

public sealed class PuzzleDbContext(
    DbContextOptions<PuzzleDbContext> options) : DbContext(options)
{
    public DbSet<Puzzle.Domain.Entities.Puzzle> Puzzles => Set<Puzzle.Domain.Entities.Puzzle>();
    public DbSet<PuzzleAttempt> PuzzleAttempts => Set<PuzzleAttempt>();
    public DbSet<PuzzleOutboxMessage> OutboxMessages => Set<PuzzleOutboxMessage>();
    public DbSet<PuzzleRatingAward> PuzzleRatingAwards => Set<PuzzleRatingAward>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var puzzle = modelBuilder.Entity<Puzzle.Domain.Entities.Puzzle>();

        puzzle.ToTable("puzzles");
        puzzle.HasKey(p => p.Id);
        puzzle.Property(p => p.Fen).HasMaxLength(120).IsRequired();
        puzzle.Property(p => p.SolutionMoves).IsRequired();
        puzzle.Property(p => p.Themes).HasMaxLength(500).IsRequired();
        puzzle.Property(p => p.Source).HasMaxLength(200);
        puzzle.HasIndex(p => new { p.IsActive, p.Rating });
        puzzle.HasIndex(p => p.ExternalPuzzleId).IsUnique().HasFilter("\"ExternalPuzzleId\" IS NOT NULL");

        var attempt = modelBuilder.Entity<PuzzleAttempt>();

        attempt.ToTable("puzzle_attempts");
        attempt.HasKey(a => a.Id);
        attempt.Property(a => a.SubmittedMoves).IsRequired();
        attempt.HasIndex(a => new { a.UserId, a.AttemptedAtUtc });

        var outbox = modelBuilder.Entity<PuzzleOutboxMessage>();
        outbox.ToTable("puzzle_outbox_messages");
        outbox.HasKey(m => m.EventId);
        outbox.Property(m => m.EventType).HasMaxLength(100).IsRequired();
        outbox.Property(m => m.Payload).IsRequired();
        outbox.Property(m => m.LastError).HasMaxLength(2000);
        outbox.HasIndex(m => new { m.PublishedAtUtc, m.CreatedAtUtc });

        attempt.HasOne(a => a.Puzzle)
            .WithMany(p => p.Attempts)
            .HasForeignKey(a => a.PuzzleId)
            .OnDelete(DeleteBehavior.Cascade);

        var ratingAward = modelBuilder.Entity<PuzzleRatingAward>();
        ratingAward.ToTable("puzzle_rating_awards");
        ratingAward.HasKey(x => new { x.UserId, x.PuzzleId });
        ratingAward.HasOne<Puzzle.Domain.Entities.Puzzle>()
            .WithMany()
            .HasForeignKey(x => x.PuzzleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
