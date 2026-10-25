using Game.Application.Contracts;
using Game.Application.Exceptions;
using Game.Application.Games;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Game.Api.Controllers;

[ApiController]
[Authorize]
[Route("games")]
public sealed class GamesController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly IGameSeekService _gameSeekService;
    public GamesController(IGameService gameService, IGameSeekService gameSeekService)
    {
        _gameService = gameService;
        _gameSeekService = gameSeekService;
    }

    [HttpPost]
    public IActionResult CreateGame([FromBody] CreateGameRequest request)
    {
        // Direct game creation lets a client force a game against another user
        // and self-assert rated status. Use matchmaking/challenge acceptance instead.
        return Conflict(new
        {
            error = "direct_game_creation_disabled",
            message = "Create games through matchmaking or an opponent-accepted challenge."
        });
    }

    [HttpPost("{gameId:guid}/moves")]
    public async Task<IActionResult> MakeMove(
        Guid gameId,
        [FromBody] MoveRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.MakeMoveAsync(
            gameId,
            userId,
            request,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                "not_your_turn" => Conflict(result),
                "timeout" => Conflict(result),
                "clock_not_found" => StatusCode(500, result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{gameId:guid}/resign")]
    public async Task<IActionResult> Resign(
    Guid gameId,
    CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.ResignAsync(
            gameId,
            userId,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    /// <summary>Cancel a live game before both players have moved (no result, not rated).</summary>
    [HttpPost("{gameId:guid}/abort")]
    public async Task<IActionResult> Abort(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.AbortAsync(
            gameId,
            userId,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                "abort_not_allowed" => Conflict(result),
                "clock_not_found" => StatusCode(500, result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{gameId:guid}/draw")]
    public async Task<IActionResult> Draw(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.OfferDrawAsync(
            gameId,
            userId,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{gameId:guid}/draw/accept")]
    public async Task<IActionResult> AcceptDraw(
    Guid gameId,
    CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.AcceptDrawAsync(
            gameId,
            userId,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                "no_draw_offer" => Conflict(result),
                "cannot_accept_own_offer" => BadRequest(result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{gameId:guid}/draw/decline")]
    public async Task<IActionResult> DeclineDraw(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.DeclineDrawAsync(
            gameId,
            userId,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                "no_draw_offer" => Conflict(result),
                "cannot_decline_own_offer" => BadRequest(result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{gameId:guid}")]
    public async Task<IActionResult> GetGame(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var game = await _gameService.GetGameAsync(
            gameId,
            userId,
            cancellationToken);

        if (game is null)
            return NotFound();

        return Ok(game);
    }

    /// <summary>Full move list with SAN and the FEN after each ply (for the move list / replay UI).</summary>
    [HttpGet("{gameId:guid}/moves")]
    public async Task<IActionResult> GetMoves(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var moves = await _gameService.GetMovesAsync(
            gameId,
            userId,
            cancellationToken);

        return moves is null ? NotFound() : Ok(moves);
    }

    /// <summary>The game as PGN text (application/x-chess-pgn) for download or import elsewhere.</summary>
    [HttpGet("{gameId:guid}/pgn")]
    public async Task<IActionResult> GetPgn(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var pgn = await _gameService.GetPgnAsync(
            gameId,
            userId,
            cancellationToken);

        return pgn is null
            ? NotFound()
            : Content(pgn, "application/x-chess-pgn; charset=utf-8");
    }

    [HttpGet]
    public async Task<IActionResult> GetGames(
    [FromQuery] string? filter,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        // Missing filter is deliberately private-by-default.
        filter = string.IsNullOrWhiteSpace(filter) ? "mine" : filter;
        var liveOnly = string.Equals(filter, "live", StringComparison.OrdinalIgnoreCase);
        var mineOnly = string.Equals(filter, "mine", StringComparison.OrdinalIgnoreCase);

        if (filter is not null && !liveOnly && !mineOnly)
        {
            return BadRequest(new
            {
                error = "invalid_filter"
            });
        }

        if (page < 1)
        {
            return BadRequest(new
            {
                error = "invalid_page",
                message = "Page must be greater than or equal to 1."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                error = "invalid_page_size",
                message = "Page size must be between 1 and 100."
            });
        }

        var games = await _gameService.GetGamesPageAsync(
            mineOnly ? userId : null,
            liveOnly,
            page,
            pageSize,
            cancellationToken);

        return Ok(games);
    }

    [HttpGet("{gameId:guid}/chat")]
    public async Task<IActionResult> GetChat(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var game = await _gameService.GetGameAsync(
            gameId,
            userId,
            cancellationToken);

        if (game is null)
            return NotFound();

        var messages = await _gameService.GetChatAsync(
            gameId,
            cancellationToken);

        return Ok(messages);
    }

    [HttpPost("{gameId:guid}/chat")]
    public async Task<IActionResult> SendChat(
        Guid gameId,
        [FromBody] ChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _gameService.SendChatMessageAsync(
    gameId,
    userId,
    request,
    cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "game_not_found" => NotFound(result),
                "not_a_player" => Forbid(),
                "game_not_live" => Conflict(result),
                _ => BadRequest(result)
            };
        }

        return Ok(result);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : Guid.Empty;
    }

    [HttpGet("{gameId:guid}/clock")]
    public async Task<IActionResult> GetClock(
    Guid gameId,
    CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        var clock = await _gameService.GetClockAsync(
            gameId,
            userId,
            cancellationToken);

        if (clock is null)
            return NotFound();

        return Ok(clock);
    }


    // The controller is [Authorize] as a whole; this one read is intentionally anonymous so
    // shared "watch this game" links work. GetPublicGameAsync only returns games flagged public.
    [AllowAnonymous]
    [HttpGet("{gameId:guid}/public")]
    public async Task<IActionResult> GetPublicGame(
    Guid gameId,
    CancellationToken cancellationToken)
    {
        var game = await _gameService.GetPublicGameAsync(
            gameId,
            cancellationToken);

        if (game is null)
            return NotFound();

        return Ok(game);
    }

    [HttpPut("{gameId:guid}/spectator-consent")]
    public async Task<IActionResult> RespondToSpectatorConsent(
      Guid gameId,
      [FromBody] RespondToSpectatorConsentRequest request,
      CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == Guid.Empty)
            return Unauthorized();

        try
        {
            var result = await _gameService.RespondToSpectatorConsentAsync(
                gameId,
                userId,
                request.AllowsSpectators,
                cancellationToken);

            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (GameConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Game was updated",
                Detail = "Another request changed this game. Refresh the game state and try again.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Spectator consent cannot be changed",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("seek")]
    public async Task<IActionResult> SeekGame(
    [FromBody] SeekGameRequest request,
    CancellationToken cancellationToken)
    {
        var playerId = GetUserId();

        if (playerId == Guid.Empty)
        {
            return Unauthorized();
        }

        var response = await _gameSeekService.CreateSeekAsync(
            playerId,
            request,
            cancellationToken);

        return Accepted(response);
    }

    [HttpGet("seek/status")]
    public async Task<IActionResult> GetSeekStatus(CancellationToken cancellationToken)
    {
        var playerId = GetUserId();
        if (playerId == Guid.Empty) return Unauthorized();
        var status = await _gameSeekService.GetStatusAsync(playerId, cancellationToken);
        return status is null ? NotFound(new { error = "seek_not_found" }) : Ok(status);
    }

    [HttpDelete("seek")]
    public async Task<IActionResult> CancelSeek(
    CancellationToken cancellationToken)
    {
        var playerId = GetUserId();

        if (playerId == Guid.Empty)
        {
            return Unauthorized();
        }

        await _gameSeekService.CancelSeekAsync(
            playerId,
            cancellationToken);

        return NoContent();
    }
}