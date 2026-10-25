using Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Infrastructure.Persistence;

public sealed class GameDbContext : DbContext
{
    public GameDbContext(
        DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    public DbSet<Game.Domain.Entities.Game> Games => Set<Game.Domain.Entities.Game>();
    public DbSet<GameClock> GameClocks => Set<GameClock>();
    public DbSet<GameMove> GameMoves => Set<GameMove>();
    public DbSet<GameChatMessage> GameChatMessages => Set<GameChatMessage>();
    public DbSet<GameSeek> GameSeeks => Set<GameSeek>();
    public DbSet<GameChallenge> GameChallenges => Set<GameChallenge>();
    public DbSet<GameRematchRequest> GameRematchRequests => Set<GameRematchRequest>();
    public DbSet<OutboxMessage> OutboxMessages =>
    Set<OutboxMessage>();
    protected override void OnModelCreating(
    ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(GameDbContext).Assembly);

        modelBuilder.Entity<GameChallenge>(e =>
        {
            e.ToTable("game_challenges"); e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.OpponentUserId, x.Status, x.CreatedAtUtc });
            e.HasIndex(x => new { x.ChallengerUserId, x.Status, x.CreatedAtUtc });
        });
        modelBuilder.Entity<GameRematchRequest>(e =>
        {
            e.ToTable("game_rematch_requests"); e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.OriginalGameId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.OpponentUserId, x.Status, x.CreatedAtUtc });
        });
    }
}