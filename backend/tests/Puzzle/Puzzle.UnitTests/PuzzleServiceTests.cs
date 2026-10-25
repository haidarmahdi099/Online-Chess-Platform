using Moq;
using Puzzle.Application.DTOs;
using Puzzle.Application.Interfaces;
using Puzzle.Application.Services;
using Puzzle.Domain.Entities;

namespace Puzzle.UnitTests;

public sealed class PuzzleServiceTests
{
    private readonly Mock<IPuzzleRepository> _repository = new();
    private readonly PuzzleService _service;

    public PuzzleServiceTests() => _service = new PuzzleService(_repository.Object);

    [Fact]
    public async Task GetNext_maps_public_fields_without_solution_moves()
    {
        var puzzle = NewPuzzle(themes: "fork,  middlegame ");
        _repository.Setup(r => r.GetNextAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(puzzle);

        var result = await _service.GetNextAsync(null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(puzzle.Id, result.Id);
        Assert.Equal(puzzle.Fen, result.Fen);
        Assert.Equal(new[] { "fork", "middlegame" }, result.Themes);
        Assert.DoesNotContain("e2e4", result.ToString());
    }

    [Fact]
    public async Task GetNext_returns_null_when_repository_has_no_active_puzzle()
    {
        _repository.Setup(r => r.GetNextAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Puzzle.Domain.Entities.Puzzle?)null);

        Assert.Null(await _service.GetNextAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetPuzzles_maps_page_and_forwards_filter_values()
    {
        var puzzle = NewPuzzle(themes: "fork,check");
        _repository.Setup(r => r.GetPageAsync(2, 5, 1200, 1800, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Puzzle.Domain.Entities.Puzzle>)new[] { puzzle }, 9));

        var result = await _service.GetPuzzlesAsync(2, 5, 1200, 1800, CancellationToken.None);

        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(9, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(new[] { "fork", "check" }, result.Items[0].Themes);
    }

    [Fact]
    public async Task SubmitAttempt_normalizes_uci_and_records_full_solution_result()
    {
        var puzzle = NewPuzzle();
        var userId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(puzzle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(puzzle);
        _repository.Setup(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), puzzle.Rating, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.SubmitAttemptAsync(puzzle.Id, userId, new[] { " E2E4 ", "E7E5", "g1f3" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsCorrect);
        Assert.True(result.RatingAffected);
        _repository.Verify(r => r.AddAttemptAsync(It.Is<PuzzleAttempt>(a =>
            a.UserId == userId && a.IsCorrect && a.SubmittedMoves == "e2e4 e7e5 g1f3"),
            puzzle.Rating, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAttempt_requires_the_entire_solution_and_marks_repeat_as_unrated()
    {
        var puzzle = NewPuzzle();
        _repository.Setup(r => r.GetByIdAsync(puzzle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(puzzle);
        _repository.Setup(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), puzzle.Rating, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.SubmitAttemptAsync(puzzle.Id, Guid.NewGuid(), new[] { "e2e4" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsCorrect);
        Assert.False(result.RatingAffected);
        _repository.Verify(r => r.AddAttemptAsync(It.Is<PuzzleAttempt>(a => !a.IsCorrect),
            puzzle.Rating, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitStep_returns_opponent_reply_without_recording_partial_attempt()
    {
        var puzzle = NewPuzzle();
        _repository.Setup(r => r.GetByIdAsync(puzzle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(puzzle);

        var result = await _service.SubmitStepAsync(puzzle.Id, Guid.NewGuid(), new[] { " E2E4 " }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("continue", result.Status);
        Assert.Equal("e7e5", result.OpponentReply);
        Assert.False(result.RatingAffected);
        _repository.Verify(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitStep_records_completed_solution_and_reveals_solution_after_wrong_move()
    {
        var puzzle = NewPuzzle();
        _repository.Setup(r => r.GetByIdAsync(puzzle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(puzzle);
        _repository.Setup(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), puzzle.Rating, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var solved = await _service.SubmitStepAsync(puzzle.Id, Guid.NewGuid(), new[] { "e2e4", "g1f3" }, CancellationToken.None);
        var failed = await _service.SubmitStepAsync(puzzle.Id, Guid.NewGuid(), new[] { "d2d4" }, CancellationToken.None);

        Assert.NotNull(solved);
        Assert.Equal("solved", solved.Status);
        Assert.True(solved.RatingAffected);
        Assert.NotNull(failed);
        Assert.Equal("failed", failed.Status);
        Assert.Equal(new[] { "e2e4", "e7e5", "g1f3" }, failed.Solution);
        _repository.Verify(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), puzzle.Rating, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Submit_methods_return_null_for_inactive_puzzle()
    {
        var puzzle = NewPuzzle();
        puzzle.IsActive = false;
        _repository.Setup(r => r.GetByIdAsync(puzzle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(puzzle);

        var attempt = await _service.SubmitAttemptAsync(puzzle.Id, Guid.NewGuid(), new[] { "e2e4" }, CancellationToken.None);
        var step = await _service.SubmitStepAsync(puzzle.Id, Guid.NewGuid(), new[] { "e2e4" }, CancellationToken.None);

        Assert.Null(attempt);
        Assert.Null(step);
        _repository.Verify(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_methods_return_null_for_missing_puzzle()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Puzzle.Domain.Entities.Puzzle?)null);

        Assert.Null(await _service.SubmitAttemptAsync(Guid.NewGuid(), Guid.NewGuid(), new[] { "e2e4" }, CancellationToken.None));
        Assert.Null(await _service.SubmitStepAsync(Guid.NewGuid(), Guid.NewGuid(), new[] { "e2e4" }, CancellationToken.None));
        _repository.Verify(r => r.AddAttemptAsync(It.IsAny<PuzzleAttempt>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Puzzle.Domain.Entities.Puzzle NewPuzzle(string themes = "fork") => new()
    {
        Fen = "initial-fen",
        SolutionMoves = "e2e4 e7e5 g1f3",
        Rating = 1500,
        Themes = themes
    };
}
