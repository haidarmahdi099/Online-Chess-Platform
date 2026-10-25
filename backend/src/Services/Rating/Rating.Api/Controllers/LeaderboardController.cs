using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rating.Application.Pagination;
using Rating.Application.Services;
using Rating.Domain.Enums;

namespace Rating.Api.Controllers;

[ApiController]
[Route("leaderboard")]
public sealed class LeaderboardController(
    IRatingService ratingService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] string category = "chess",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!PageWindow.TryGetOffset(page, pageSize, out _))
            return BadRequest(new { error = "Page and page size are outside the supported range." });
        var ratingCategory = category.Trim().ToLowerInvariant() switch
        {
            "chess" => RatingCategory.Chess,
            "puzzle" => RatingCategory.Puzzle,
            "bullet" => RatingCategory.Bullet,
            "blitz" => RatingCategory.Blitz,
            "rapid" => RatingCategory.Rapid,
            "classical" => RatingCategory.Classical,
            _ => (RatingCategory?)null
        };
        if (ratingCategory is null)
            return BadRequest(new { error = "Category must be 'chess', 'puzzle', 'bullet', 'blitz', 'rapid' or 'classical'." });
        return Ok(await ratingService.GetLeaderboardPageAsync(ratingCategory.Value, page, pageSize, cancellationToken));
    }
}
