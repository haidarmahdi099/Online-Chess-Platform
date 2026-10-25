using Game.Application.Games;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace Game.Api.Controllers;

[ApiController]
[Route("internal/tournament-games")]
public sealed class InternalTournamentGamesController(IGameService gameService, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTournamentGameRequest request, CancellationToken ct)
    {
        var expected = configuration["InternalServices:TournamentKey"];
        var supplied = Request.Headers["X-Internal-Service-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied)))
            return Unauthorized();
        if (request.GameId == Guid.Empty || request.WhitePlayerId == Guid.Empty || request.BlackPlayerId == Guid.Empty ||
            request.WhitePlayerId == request.BlackPlayerId || request.InitialTimeSeconds is < 10 or > 7200 || request.IncrementSeconds is < 0 or > 300)
            return BadRequest(new { message = "Invalid tournament game request." });
        try
        {
            var id = await gameService.CreateTournamentGameAsync(request.GameId, new CreateGameRequest(request.WhitePlayerId, request.BlackPlayerId, request.InitialTimeSeconds, request.IncrementSeconds, false), ct);
            return Ok(new { gameId = id });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public sealed record CreateTournamentGameRequest(Guid GameId, Guid WhitePlayerId, Guid BlackPlayerId, int InitialTimeSeconds, int IncrementSeconds);
