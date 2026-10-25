using Tournament.Domain.Enums;

namespace Tournament.Domain.Entities;

public sealed class ChessTournament
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string? Description { get; set; }

    public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;

    // Time control: base time plus per-move increment, both in seconds.
    public int InitialTimeSeconds { get; set; } = 180;

    public int IncrementSeconds { get; set; }

    public int MaxParticipants { get; set; } = 64;

    public DateTime StartsAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Swiss lifecycle. TotalRounds is fixed when the tournament starts;
    // CurrentRound is 0 until then.
    public int TotalRounds { get; set; }

    public int CurrentRound { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<TournamentParticipant> Participants { get; set; } =
        new List<TournamentParticipant>();

    public ICollection<TournamentMatch> Matches { get; set; } = new List<TournamentMatch>();

    // Pure business rule: can a player take another seat right now?
    // The caller supplies the current counts so this stays free of persistence.
    public JoinTournamentOutcome EvaluateJoin(
        int currentParticipantCount,
        bool userAlreadyJoined,
        DateTime nowUtc)
    {
        if (userAlreadyJoined)
            return JoinTournamentOutcome.AlreadyJoined;

        if (Status != TournamentStatus.Upcoming || nowUtc >= StartsAtUtc)
            return JoinTournamentOutcome.RegistrationClosed;

        if (currentParticipantCount >= MaxParticipants)
            return JoinTournamentOutcome.TournamentFull;

        return JoinTournamentOutcome.Joined;
    }
}
