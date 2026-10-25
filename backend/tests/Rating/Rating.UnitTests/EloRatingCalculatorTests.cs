using Rating.Application.Services;

namespace Rating.UnitTests;

public sealed class EloRatingCalculatorTests
{
    private readonly EloRatingCalculator _calculator = new();

    [Theory]
    [InlineData(1200, 1200, 1.0, 16)]
    [InlineData(1200, 1200, 0.5, 0)]
    [InlineData(1200, 1200, 0.0, -16)]
    public void Chess_rating_change_is_zero_sum_for_equal_ratings(int player, int opponent, double score, int expected)
    {
        Assert.Equal(expected, _calculator.CalculateChessRatingChange(player, opponent, score));
    }

    [Fact]
    public void Chess_favorite_gains_less_for_a_win_than_an_underdog()
    {
        var favoriteGain = _calculator.CalculateChessRatingChange(1800, 1200, 1.0);
        var underdogGain = _calculator.CalculateChessRatingChange(1200, 1800, 1.0);

        Assert.True(favoriteGain < underdogGain);
        Assert.True(favoriteGain > 0);
        Assert.True(underdogGain > 16);
    }

    [Fact]
    public void Puzzle_change_uses_difficulty_and_correctness()
    {
        var correctAgainstHigherRatedPuzzle = _calculator.CalculatePuzzleRatingChange(1200, 1800, true);
        var incorrectAgainstLowerRatedPuzzle = _calculator.CalculatePuzzleRatingChange(1800, 1200, false);

        Assert.True(correctAgainstHigherRatedPuzzle > 0);
        Assert.True(incorrectAgainstLowerRatedPuzzle < 0);
    }

    [Theory]
    [InlineData(-1, 1200, 0.5, 32)]
    [InlineData(1200, -1, 0.5, 32)]
    [InlineData(1200, 1200, -0.1, 32)]
    [InlineData(1200, 1200, 1.1, 32)]
    public void Chess_calculation_rejects_invalid_inputs(int player, int opponent, double score, int kFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.CalculateChessRatingChange(player, opponent, score, kFactor));
    }

    [Fact]
    public void Rating_floor_prevents_negative_ratings_and_tracks_results()
    {
        var rating = new Rating.Domain.Entities.PlayerRating(Guid.NewGuid(), Rating.Domain.Enums.RatingCategory.Chess, 2);

        rating.ApplyResult(-20, Rating.Domain.Enums.GameResult.Loss);

        Assert.Equal(0, rating.Rating);
        Assert.Equal(1, rating.GamesPlayed);
        Assert.Equal(1, rating.Losses);
    }
}
