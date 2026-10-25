namespace Tournament.Application.Interfaces;

public enum StartTournamentOutcome
{
    Started,
    /// <summary>Auto-start found fewer than two players and cancelled the tournament.</summary>
    Cancelled,
    NotFound,
    InvalidState,
    TooEarly,
    NotEnoughPlayers
}

public enum RoundAdvanceOutcome
{
    NotFound,
    InvalidState,
    RoundInProgress,
    NextRoundCreated,
    Completed
}

public enum MatchResultStatus
{
    Recorded,
    AlreadyRecorded,
    /// <summary>The finished game does not belong to any tournament match.</summary>
    NotATournamentGame,
    InvalidResult,
    MatchNotFound,
    InvalidState
}

public sealed record MatchResultOutcome(
    MatchResultStatus Status,
    Guid? TournamentId = null,
    RoundAdvanceOutcome? Advance = null);

public enum CompleteTournamentOutcome
{
    Completed,
    NotFound,
    InvalidState,
    NotReady
}
