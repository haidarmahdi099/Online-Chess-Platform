using Analysis.Application;
using Analysis.Application.DTOs;
using Analysis.Application.Exceptions;
using Analysis.Application.Services;
using Analysis.Domain.Chess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Analysis.Api.Controllers;

[ApiController]
[Route("analysis")]
public sealed class AnalysisController(IAnalysisService analysisService)
    : ControllerBase
{
    [HttpPost("evaluate")]
    [Authorize]
    [ProducesResponseType<EvaluatePositionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluatePositionDto request,
        CancellationToken cancellationToken)
    {
        if (request.Depth is < AnalysisLimits.MinDepth or > AnalysisLimits.MaxDepth)
        {
            return BadRequest(new
            {
                message = $"Depth must be between {AnalysisLimits.MinDepth} and {AnalysisLimits.MaxDepth}."
            });
        }

        if (!FenPosition.TryCreate(request.Fen, out var position, out var error))
            return BadRequest(new { message = error });

        try
        {
            var result = await analysisService.EvaluateAsync(
                position!, request.Depth, cancellationToken);

            return Ok(result);
        }
        catch (EngineUnavailableException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = ex.Message });
        }
    }
}
