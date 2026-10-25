using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rating.Application.Pagination;
using Rating.Application.Services;
using Rating.Domain.Enums;

namespace Rating.Api.Controllers;

/// <summary>Public per-player rating data for profile pages (same visibility as the leaderboard).</summary>
[ApiController]
[Route("ratings")]
public sealed class RatingsController(
    IRatingService ratingService) : ControllerBase
{
    /// <summary>Current chess and puzzle rating. Players with no games yet report the default 1200.</summary>
    [AllowAnonymous]
    [HttpGet("{playerId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlayerRatings(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var ratings = await ratingService.GetPlayerRatingsAsync(
            playerId,
            cancellationToken);

        return Ok(ratings);
    }

    /// <summary>Rating changes, newest first. Use for the rating graph and recent-results list.</summary>
    [AllowAnonymous]
    [HttpGet("{playerId:guid}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRatingHistory(
        Guid playerId,
        [FromQuery] string category = "chess",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
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
            return BadRequest(new
            {
                error = "Category must be 'chess', 'puzzle', 'bullet', 'blitz', 'rapid' or 'classical'."
            });

        if (!PageWindow.TryGetOffset(page, pageSize, out _))
            return BadRequest(new
            {
                error = "Page and page size are outside the supported range."
            });

        var history = await ratingService.GetRatingHistoryAsync(
            playerId,
            ratingCategory.Value,
            page,
            pageSize,
            cancellationToken);

        return Ok(history);
    }
}
