namespace Analysis.Domain.Evaluation;

// Score is expressed from the point of view of the side to move
// (the same convention UCI engines use). For Mate, Score is the number of
// moves to mate: positive means the side to move mates, negative means it
// is being mated.
public sealed record PositionEvaluation(
    ScoreType ScoreType,
    int Score,
    int Depth,
    string? BestMove,
    IReadOnlyList<string> PrincipalVariation)
{
    // Converts a side-to-move score into a White-perspective score.
    public PositionEvaluation ToWhitePerspective(bool isWhiteToMove) =>
        isWhiteToMove ? this : this with { Score = -Score };
}
