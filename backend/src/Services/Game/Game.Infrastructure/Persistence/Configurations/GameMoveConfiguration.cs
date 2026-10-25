using Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Game.Infrastructure.Persistence.Configurations;

public sealed class GameMoveConfiguration
    : IEntityTypeConfiguration<GameMove>
{
    public void Configure(EntityTypeBuilder<GameMove> builder)
    {
        builder.ToTable("game_moves");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.GameId)
            .IsRequired();

        builder.Property(x => x.PlayerId)
            .IsRequired();

        builder.Property(x => x.MoveNumber)
            .IsRequired();

        builder.Property(x => x.From)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.To)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.Promotion)
            .IsRequired(false)
            .HasMaxLength(1);

        builder.Property(x => x.FenAfterMove)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.GameId,
            x.MoveNumber
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.GameId,
            x.CreatedAt
        });
    }
}