using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tournament.Infrastructure.Persistence;

#nullable disable

namespace Tournament.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TournamentDbContext))]
public sealed class TournamentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");
        modelBuilder.Entity("Tournament.Domain.Entities.ChessTournament", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<Guid>("CreatedByUserId").HasColumnType("uuid");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("CompletedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<int>("CurrentRound").HasColumnType("integer");
            b.Property<DateTime?>("StartedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<int>("TotalRounds").HasColumnType("integer");
            b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<int>("IncrementSeconds").HasColumnType("integer");
            b.Property<int>("InitialTimeSeconds").HasColumnType("integer");
            b.Property<int>("MaxParticipants").HasColumnType("integer");
            b.Property<string>("Name").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
            b.Property<DateTime>("StartsAtUtc").HasColumnType("timestamp with time zone");
            b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.HasKey("Id");
            b.HasIndex("Status", "StartsAtUtc");
            b.ToTable("tournaments");
        });
        modelBuilder.Entity("Tournament.Infrastructure.Persistence.TournamentOutboxMessage", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("uuid");
            b.Property<string>("EventType").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
            b.Property<Guid>("TournamentId").HasColumnType("uuid");
            b.Property<string>("DataJson").IsRequired().HasColumnType("text");
            b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<int>("Attempts").HasColumnType("integer");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("PublishedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<string>("LastError").HasMaxLength(2000).HasColumnType("character varying(2000)");
            b.HasKey("EventId");
            b.HasIndex("Status", "CreatedAtUtc");
            b.ToTable("tournament_outbox_messages");
        });
        modelBuilder.Entity("Tournament.Domain.Entities.TournamentParticipant", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<DateTime>("JoinedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<Guid>("TournamentId").HasColumnType("uuid");
            b.Property<Guid>("UserId").HasColumnType("uuid");
            b.HasKey("Id");
            b.HasIndex("TournamentId", "UserId").IsUnique();
            b.HasIndex("UserId");
            b.ToTable("tournament_participants");
        });
        modelBuilder.Entity("Tournament.Domain.Entities.TournamentMatch", b =>
        {
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<Guid?>("BlackPlayerId").HasColumnType("uuid");
            b.Property<Guid?>("GameId").HasColumnType("uuid");
            b.Property<DateTime?>("FinishedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<string>("LastProvisionError").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<DateTime?>("NextProvisionAtUtc").HasColumnType("timestamp with time zone");
            b.Property<int>("ProvisionAttempts").HasColumnType("integer");
            b.Property<string>("Result").HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<Guid?>("WinnerId").HasColumnType("uuid");
            b.Property<int>("RoundNumber").HasColumnType("integer");
            b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<Guid>("TournamentId").HasColumnType("uuid");
            b.Property<Guid>("WhitePlayerId").HasColumnType("uuid");
            b.HasKey("Id");
            b.HasIndex("GameId").IsUnique();
            b.HasIndex("TournamentId", "RoundNumber");
            b.ToTable("tournament_matches");
        });
        modelBuilder.Entity("Tournament.Infrastructure.Persistence.TournamentOutboxMessage", b =>
        {
            b.Property<Guid>("EventId").HasColumnType("uuid");
            b.Property<string>("EventType").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
            b.Property<Guid>("TournamentId").HasColumnType("uuid");
            b.Property<string>("DataJson").IsRequired().HasColumnType("text");
            b.Property<string>("Status").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<int>("Attempts").HasColumnType("integer");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("PublishedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<DateTime?>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
            b.Property<string>("LastError").HasMaxLength(2000).HasColumnType("character varying(2000)");
            b.HasKey("EventId");
            b.HasIndex("Status", "CreatedAtUtc");
            b.ToTable("tournament_outbox_messages");
        });
        modelBuilder.Entity("Tournament.Domain.Entities.TournamentParticipant", b =>
        {
            b.HasOne("Tournament.Domain.Entities.ChessTournament", "Tournament")
                .WithMany("Participants").HasForeignKey("TournamentId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Tournament");
        });
        modelBuilder.Entity("Tournament.Domain.Entities.TournamentMatch", b =>
        {
            b.HasOne("Tournament.Domain.Entities.ChessTournament", "Tournament")
                .WithMany("Matches").HasForeignKey("TournamentId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("Tournament");
        });
        modelBuilder.Entity("Tournament.Domain.Entities.ChessTournament", b =>
        {
            b.Navigation("Matches");
            b.Navigation("Participants");
        });
    }
}
