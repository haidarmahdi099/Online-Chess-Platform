using Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Game.Infrastructure.Persistence.Configurations;

public sealed class GameSeekConfiguration
    : IEntityTypeConfiguration<GameSeek>
{
    public void Configure(EntityTypeBuilder<GameSeek> builder)
    {
        builder.ToTable("game_seeks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.PlayerId)
            .IsRequired();

        builder.Property(x => x.InitialTimeSeconds)
            .IsRequired();

        builder.Property(x => x.IncrementSeconds)
            .IsRequired();

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Rated)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.MatchedGameId)
            .IsRequired(false);

        builder.HasIndex(x => new
        {
            x.IsActive,
            x.Rated,
            x.Category,
            x.InitialTimeSeconds,
            x.IncrementSeconds,
            x.CreatedAt
        });

        // A player should have at most one active search.
        builder.HasIndex(x => x.PlayerId)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");
    }
}