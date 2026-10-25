namespace Rating.Application.Services;

public sealed class EloRatingCalculator : IRatingCalculator
{
    public int CalculateChessRatingChange(
        int playerRating,
        int opponentRating,
        double score,
        int kFactor = 32)
    {
        ValidateRatings(playerRating, opponentRating);
        ValidateKFactor(kFactor);

        if (score is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(score));

        double expectedScore =
            1.0 / (1.0 + Math.Pow(10.0, (opponentRating - playerRating) / 400.0));

        return (int)Math.Round(
            kFactor * (score - expectedScore),
            MidpointRounding.AwayFromZero);
    }

    public int CalculatePuzzleRatingChange(
        int playerRating,
        int puzzleRating,
        bool isCorrect,
        int kFactor = 24)
    {
        ValidateRatings(playerRating, puzzleRating);
        ValidateKFactor(kFactor);

        double expectedScore =
            1.0 / (1.0 + Math.Pow(10.0, (puzzleRating - playerRating) / 400.0));

        double actualScore = isCorrect ? 1.0 : 0.0;

        return (int)Math.Round(
            kFactor * (actualScore - expectedScore),
            MidpointRounding.AwayFromZero);
    }

    private static void ValidateRatings(int playerRating, int opponentRating)
    {
        if (playerRating < 0)
            throw new ArgumentOutOfRangeException(nameof(playerRating));

        if (opponentRating < 0)
            throw new ArgumentOutOfRangeException(nameof(opponentRating));
    }

    private static void ValidateKFactor(int kFactor)
    {
        if (kFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(kFactor));
    }
}