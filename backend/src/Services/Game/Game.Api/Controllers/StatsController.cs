using Game.Application.Games;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Game.Api.Controllers;

[ApiController]
[Authorize]
[Route("stats")]
public sealed class StatsController : ControllerBase
{
    private readonly IGameService _gameService;

    public StatsController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet]
    public async Task<IActionResult> GetStats(
        CancellationToken cancellationToken)
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
            return Unauthorized();

        var stats = await _gameService.GetStatsAsync(
            userId,
            cancellationToken);

        return Ok(stats);
    }
    [AllowAnonymous]
    [HttpGet("{playerId:guid}")]
    public async Task<IActionResult> GetPublicStats(Guid playerId, CancellationToken cancellationToken)
    {
        if (playerId == Guid.Empty) return BadRequest(new { code = "invalid_player_id" });
        var stats = await _gameService.GetStatsAsync(playerId, cancellationToken);
        return Ok(new { playerId, stats });
    }

}