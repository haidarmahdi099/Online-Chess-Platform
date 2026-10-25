using Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Game.Infrastructure.Persistence.Configurations;

public sealed class GameClockConfiguration
    : IEntityTypeConfiguration<GameClock>
{
    public void Configure(EntityTypeBuilder<GameClock> builder)
    {
        builder.ToTable("game_clocks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        builder.Property(c => c.Version)
            .IsRowVersion();

        builder.Property(x => x.GameId)
            .IsRequired();

        builder.Property(x => x.WhiteRemainingMilliseconds)
            .IsRequired();

        builder.Property(x => x.BlackRemainingMilliseconds)
            .IsRequired();

        builder.Property(x => x.TurnStartedAt)
            .IsRequired(false);

        builder.Property(x => x.WhiteToMove)
            .IsRequired();

        builder.HasIndex(x => x.GameId)
            .IsUnique();
    }
}