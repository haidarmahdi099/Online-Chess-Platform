using Analysis.Application.DTOs;
using Analysis.Domain.Chess;

namespace Analysis.Application.Services;

public interface IAnalysisService
{
    Task<EvaluatePositionResultDto> EvaluateAsync(
        FenPosition position,
        int? depth,
        CancellationToken cancellationToken);
}
