using Analysis.Application.DTOs;
using Analysis.Application.Interfaces;
using Analysis.Domain.Chess;
using Analysis.Domain.Evaluation;

namespace Analysis.Application.Services;

public sealed class AnalysisService(IChessEngine engine) : IAnalysisService
{
    public async Task<EvaluatePositionResultDto> EvaluateAsync(
        FenPosition position,
        int? depth,
        CancellationToken cancellationToken)
    {
        var requestedDepth = Math.Clamp(
            depth ?? AnalysisLimits.DefaultDepth,
            AnalysisLimits.MinDepth,
            AnalysisLimits.MaxDepth);

        var evaluation = await engine.EvaluateAsync(
            position, requestedDepth, cancellationToken);

        // Engines report from the side to move; clients want a stable
        // White-perspective score so an evaluation bar never flips sides.
        var white = evaluation.ToWhitePerspective(position.IsWhiteToMove);

        return new EvaluatePositionResultDto(
            position.Fen,
            white.Depth,
            white.ScoreType == ScoreType.Mate ? "mate" : "cp",
            white.Score,
            white.BestMove,
            white.PrincipalVariation.ToArray());
    }
}
