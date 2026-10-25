using Puzzle.Application.DTOs;
using Puzzle.Application.Interfaces;
using Puzzle.Domain.Entities;


namespace Puzzle.Application.Services;


public sealed class PuzzleService(
    IPuzzleRepository repository)
    : IPuzzleService

{
    public async Task<PuzzleDto?> GetNextAsync(
        Guid? excludePuzzleId,
        CancellationToken cancellationToken)
    {
        var puzzle = await repository.GetNextAsync(
            excludePuzzleId,
            cancellationToken);

        return puzzle is null ? null : ToPuzzleDto(puzzle);
    }

    public async Task<PuzzlePageDto> GetPuzzlesAsync(
        int page,
        int pageSize,
        int? minRating,
        int? maxRating,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.GetPageAsync(
            page, pageSize, minRating, maxRating, cancellationToken);

        return new PuzzlePageDto(
            items.Select(p => new PuzzleListItemDto(
                p.Id, p.Rating, ParseThemes(p.Themes))).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<PuzzleAttemptResultDto?> SubmitAttemptAsync(
        Guid puzzleId,
        Guid userId,
        string[] moves,
        CancellationToken cancellationToken)
    {
        var puzzle = await repository.GetByIdAsync(
            puzzleId, cancellationToken);

        if (puzzle is null || !puzzle.IsActive)
            return null;

        var solutionMoves = NormalizeMoves(
    puzzle.SolutionMoves.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries));

        var submitted = NormalizeMoves(moves);

        // Puzzle.Fen is the position after Lichess' first (opponent setup) move, and
        // SolutionMoves contains only moves from the side to solve the puzzle.
        var expected = solutionMoves;

        var isCorrect = submitted.Length > 0 &&
                        submitted.SequenceEqual(
                            expected,
                            StringComparer.OrdinalIgnoreCase);

        var attempt = new PuzzleAttempt
        {
            PuzzleId = puzzle.Id,
            UserId = userId,
            SubmittedMoves = string.Join(' ', submitted),
            IsCorrect = isCorrect
        };
        var rated = await repository.AddAttemptAsync(attempt, puzzle.Rating, cancellationToken);

        return new PuzzleAttemptResultDto(
            puzzle.Id,
            isCorrect,
            isCorrect ? "Correct solution." : "Incorrect solution.",
            rated);
    }

    public async Task<PuzzleStepResultDto?> SubmitStepAsync(
        Guid puzzleId,
        Guid userId,
        string[] userMoves,
        CancellationToken cancellationToken)
    {
        var puzzle = await repository.GetByIdAsync(
            puzzleId, cancellationToken);

        if (puzzle is null || !puzzle.IsActive)
            return null;

        var solutionMoves = NormalizeMoves(
            puzzle.SolutionMoves.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));

        // The importer stores the post-setup FEN and the full remaining line, starting
        // with the user's first solution move.
        var expected = solutionMoves;

        var submitted = NormalizeMoves(userMoves);
        var lastUserIndex = 2 * (submitted.Length - 1);

        var onTrack = submitted.Length > 0 && lastUserIndex < expected.Length;
        for (var i = 0; onTrack && i < submitted.Length; i++)
        {
            onTrack = submitted[i] == expected[2 * i];
        }

        if (onTrack && lastUserIndex < expected.Length - 1)
        {
            // Correct so far and the puzzle continues: nothing is stored until it ends.
            return new PuzzleStepResultDto(
                puzzle.Id,
                "continue",
                expected[lastUserIndex + 1],
                null,
                "Correct, keep going.",
                false);
        }

        var solved = onTrack;

        var attempt = new PuzzleAttempt
        {
            PuzzleId = puzzle.Id,
            UserId = userId,
            SubmittedMoves = string.Join(' ', submitted),
            IsCorrect = solved
        };

        var rated = await repository.AddAttemptAsync(attempt, puzzle.Rating, cancellationToken);

        return new PuzzleStepResultDto(
            puzzle.Id,
            solved ? "solved" : "failed",
            null,
            solved ? null : expected,
            solved ? "Puzzle solved." : "Incorrect move.",
            rated);
    }

    private static PuzzleDto ToPuzzleDto(
    Puzzle.Domain.Entities.Puzzle puzzle)
    {
        if (string.IsNullOrWhiteSpace(puzzle.SolutionMoves))
            throw new InvalidOperationException($"Puzzle {puzzle.Id} has no solution moves.");

        return new PuzzleDto(
            puzzle.Id,
            puzzle.Fen,
            InitialMove: null,
            puzzle.Rating,
            ParseThemes(puzzle.Themes));
    }

    private static string[] ParseThemes(string themes) =>
        themes.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

    private static string[] NormalizeMoves(IEnumerable<string> moves) =>
        moves.Select(m => m.Trim().ToLowerInvariant()).ToArray();
}
