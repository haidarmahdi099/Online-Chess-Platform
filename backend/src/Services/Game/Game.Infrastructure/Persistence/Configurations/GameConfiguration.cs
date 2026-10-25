using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Game.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game.Domain.Entities.Game>
{
    public void Configure(EntityTypeBuilder<Game.Domain.Entities.Game> builder)
    {
        builder.ToTable("games");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        builder.Property(g => g.Version)
    .IsRowVersion();

        builder.Property(x => x.WhitePlayerId)
            .IsRequired();

        builder.Property(x => x.BlackPlayerId)
            .IsRequired();

        builder.Property(x => x.Fen)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.WinnerId)
            .IsRequired(false);

        builder.Property(x => x.EndReason)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .IsRequired(false);

        builder.Property(x => x.FinishedAt)
            .IsRequired(false);

        builder.Property(x => x.InitialTimeSeconds)
            .IsRequired();

        builder.Property(x => x.IncrementSeconds)
            .IsRequired();

        builder.HasIndex(x => x.WhitePlayerId);
        builder.HasIndex(x => x.BlackPlayerId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
    .HasDatabaseName("ix_games_status_created_at");

        builder.Property(x => x.WhiteSpectatorConsent)
         .IsRequired()
         .HasDefaultValue(false);

        builder.Property(x => x.BlackSpectatorConsent)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.SpectatorConsentRejected)
            .IsRequired()
            .HasDefaultValue(false);

        // These are calculated from the entity's state, not database columns.
        builder.Ignore(x => x.IsPublic);
        builder.Ignore(x => x.SpectatorConsentResolved);

        builder.Property(x => x.Rated)
    .IsRequired()
    .HasDefaultValue(false);
    }
}