using Analysis.Domain.Chess;
using Analysis.Domain.Evaluation;

namespace Analysis.Application.Interfaces;

public interface IChessEngine
{
    // Returns the evaluation from the side-to-move perspective.
    // Throws EngineUnavailableException when the engine cannot answer.
    Task<PositionEvaluation> EvaluateAsync(
        FenPosition position,
        int depth,
        CancellationToken cancellationToken);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
