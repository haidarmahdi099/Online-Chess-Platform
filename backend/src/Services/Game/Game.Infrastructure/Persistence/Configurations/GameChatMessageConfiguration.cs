using Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Game.Infrastructure.Persistence.Configurations;

public sealed class GameChatMessageConfiguration
    : IEntityTypeConfiguration<GameChatMessage>
{
    public void Configure(
        EntityTypeBuilder<GameChatMessage> builder)
    {
        builder.ToTable("game_chat_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.GameId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.GameId,
            x.CreatedAt
        });
    }
}