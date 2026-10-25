namespace Tournament.Domain.Entities;

public sealed class TournamentParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }

    public Guid UserId { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public ChessTournament Tournament { get; set; } = null!;
}
