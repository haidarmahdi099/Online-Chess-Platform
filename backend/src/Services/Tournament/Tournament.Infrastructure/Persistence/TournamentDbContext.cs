using Microsoft.EntityFrameworkCore;
using Tournament.Domain.Entities;

namespace Tournament.Infrastructure.Persistence;

public sealed class TournamentDbContext(
    DbContextOptions<TournamentDbContext> options) : DbContext(options)
{
    public DbSet<ChessTournament> Tournaments => Set<ChessTournament>();
    public DbSet<TournamentParticipant> TournamentParticipants => Set<TournamentParticipant>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();
    public DbSet<TournamentOutboxMessage> OutboxMessages => Set<TournamentOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var outbox = modelBuilder.Entity<TournamentOutboxMessage>();
        outbox.ToTable("tournament_outbox_messages");
        outbox.HasKey(x => x.EventId);
        outbox.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        outbox.Property(x => x.Status).HasMaxLength(20).IsRequired();
        outbox.Property(x => x.LastError).HasMaxLength(2000);
        outbox.HasIndex(x => new { x.Status, x.CreatedAtUtc });

        var tournament = modelBuilder.Entity<ChessTournament>();

        tournament.ToTable("tournaments");
        tournament.HasKey(t => t.Id);
        tournament.Property(t => t.Name).HasMaxLength(120).IsRequired();
        tournament.Property(t => t.CreatedByUserId).IsRequired();
        tournament.Property(t => t.Description).HasMaxLength(1000);
        tournament.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        tournament.HasIndex(t => new { t.Status, t.StartsAtUtc });

        var participant = modelBuilder.Entity<TournamentParticipant>();

        participant.ToTable("tournament_participants");
        participant.HasKey(p => p.Id);
        // A player can hold only one seat per tournament, even under concurrent requests.
        participant.HasIndex(p => new { p.TournamentId, p.UserId }).IsUnique();
        participant.HasIndex(p => p.UserId);

        var match = modelBuilder.Entity<TournamentMatch>();
        match.ToTable("tournament_matches");
        match.HasKey(m => m.Id);
        match.Property(m => m.Status).HasMaxLength(20).IsRequired();
        match.Property(m => m.Result).HasMaxLength(20);
        match.Property(m => m.LastProvisionError).HasMaxLength(200);
        match.HasIndex(m => new { m.TournamentId, m.RoundNumber });
        match.HasIndex(m => m.GameId).IsUnique();
        match.HasOne(m => m.Tournament).WithMany(t => t.Matches)
            .HasForeignKey(m => m.TournamentId).OnDelete(DeleteBehavior.Cascade);

        participant.HasOne(p => p.Tournament)
            .WithMany(t => t.Participants)
            .HasForeignKey(p => p.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
