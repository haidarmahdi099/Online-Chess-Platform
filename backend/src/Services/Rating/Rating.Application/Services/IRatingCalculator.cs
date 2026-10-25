namespace Rating.Application.Services;

public interface IRatingCalculator
{
    int CalculateChessRatingChange(
        int playerRating,
        int opponentRating,
        double score,
        int kFactor = 32);

    int CalculatePuzzleRatingChange(
        int playerRating,
        int puzzleRating,
        bool isCorrect,
        int kFactor = 24);
}
