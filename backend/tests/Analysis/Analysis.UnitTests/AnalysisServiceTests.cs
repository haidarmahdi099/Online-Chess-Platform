using Analysis.Application;
using Analysis.Application.Interfaces;
using Analysis.Application.Services;
using Analysis.Domain.Chess;
using Analysis.Domain.Evaluation;
using Moq;

namespace Analysis.UnitTests;

public sealed class AnalysisServiceTests
{
    private readonly Mock<IChessEngine> _engine = new();
    private readonly AnalysisService _service;

    public AnalysisServiceTests() => _service = new AnalysisService(_engine.Object);

    [Fact]
    public async Task Evaluate_defaults_depth_and_returns_white_perspective_for_white_to_move()
    {
        var position = CreatePosition("w");
        _engine.Setup(e => e.EvaluateAsync(position, AnalysisLimits.DefaultDepth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PositionEvaluation(ScoreType.Centipawns, 42, 10, "e2e4", new[] { "e2e4", "e7e5" }));

        var result = await _service.EvaluateAsync(position, null, CancellationToken.None);

        Assert.Equal(10, result.Depth); // Result depth is the depth actually reached by the engine.
        Assert.Equal("cp", result.ScoreType);
        Assert.Equal(42, result.Score);
        Assert.Equal("e2e4", result.BestMove);
        Assert.Equal(new[] { "e2e4", "e7e5" }, result.PrincipalVariation);
    }

    [Fact]
    public async Task Evaluate_negates_side_to_move_score_when_black_is_to_move()
    {
        var position = CreatePosition("b");
        _engine.Setup(e => e.EvaluateAsync(position, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PositionEvaluation(ScoreType.Centipawns, 35, 8, "e7e5", new[] { "e7e5" }));

        var result = await _service.EvaluateAsync(position, 8, CancellationToken.None);

        Assert.Equal(-35, result.Score);
        Assert.Equal("e7e5", result.BestMove);
    }

    [Theory]
    [InlineData(-10, 1)]
    [InlineData(100, 20)]
    public async Task Evaluate_clamps_depth_at_application_boundary(int requestedDepth, int expectedDepth)
    {
        var position = CreatePosition("w");
        _engine.Setup(e => e.EvaluateAsync(position, expectedDepth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PositionEvaluation(ScoreType.Mate, 3, expectedDepth, "f7f8q", new[] { "f7f8q" }));

        var result = await _service.EvaluateAsync(position, requestedDepth, CancellationToken.None);

        Assert.Equal(expectedDepth, result.Depth);
        Assert.Equal("mate", result.ScoreType);
        Assert.Equal(3, result.Score);
        _engine.Verify(e => e.EvaluateAsync(position, expectedDepth, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Evaluate_forwards_cancellation_token_to_engine()
    {
        var position = CreatePosition("w");
        using var source = new CancellationTokenSource();
        _engine.Setup(e => e.EvaluateAsync(position, AnalysisLimits.DefaultDepth, source.Token))
            .ReturnsAsync(new PositionEvaluation(ScoreType.Centipawns, 0, 12, null, Array.Empty<string>()));

        await _service.EvaluateAsync(position, null, source.Token);

        _engine.Verify(e => e.EvaluateAsync(position, AnalysisLimits.DefaultDepth, source.Token), Times.Once);
    }

    private static FenPosition CreatePosition(string sideToMove)
    {
        var fen = $"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR {sideToMove} KQkq - 0 1";
        Assert.True(FenPosition.TryCreate(fen, out var position, out var error), error);
        return position!;
    }
}
