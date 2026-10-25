using System.Security.Claims;
using Game.Application.Games;
using Game.Domain.Entities;
using Game.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Game.Api.Controllers;

[ApiController, Authorize, Route("games")]
public sealed class RematchesController(GameDbContext db, IGameService games) : ControllerBase
{
    private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    [HttpPost("{gameId:guid}/rematch")]
    public async Task<IActionResult> RequestRematch(Guid gameId, CancellationToken ct)
    {
        var me=CurrentUserId; if(me==Guid.Empty)return Unauthorized();
        var game=await games.GetGameAsync(gameId,me,ct); if(game is null)return NotFound();
        if(game.Status is "Live" or "Waiting")return Conflict(new{code="game_not_finished"});
        var opponent=game.WhitePlayerId==me?game.BlackPlayerId:game.BlackPlayerId==me?game.WhitePlayerId:Guid.Empty;
        if(opponent==Guid.Empty)return Forbid();
        var duplicate=await db.GameRematchRequests.AnyAsync(x=>x.OriginalGameId==gameId&&x.RequesterUserId==me&&x.Status=="Pending"&&x.ExpiresAtUtc>DateTime.UtcNow,ct);
        if(duplicate)return Conflict(new{code="rematch_already_pending"});
        var entity=new GameRematchRequest{Id=Guid.NewGuid(),OriginalGameId=gameId,RequesterUserId=me,OpponentUserId=opponent,InitialTimeSeconds=game.InitialTimeSeconds,IncrementSeconds=game.IncrementSeconds,Rated=game.Rated,Status="Pending",CreatedAtUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddMinutes(10)};
        db.GameRematchRequests.Add(entity);await db.SaveChangesAsync(ct);
        return Accepted(new{id=entity.Id,originalGameId=gameId,requesterUserId=me,opponentUserId=opponent,status=entity.Status,expiresAtUtc=entity.ExpiresAtUtc});
    }

    [HttpGet("rematches/incoming")]
    public async Task<IActionResult> Incoming(CancellationToken ct){var me=CurrentUserId;if(me==Guid.Empty)return Unauthorized();await Expire(ct);return Ok(await db.GameRematchRequests.AsNoTracking().Where(x=>x.OpponentUserId==me&&x.Status=="Pending").OrderByDescending(x=>x.CreatedAtUtc).Select(x=>new{id=x.Id,originalGameId=x.OriginalGameId,requesterUserId=x.RequesterUserId,initialTimeSeconds=x.InitialTimeSeconds,incrementSeconds=x.IncrementSeconds,rated=x.Rated,createdAtUtc=x.CreatedAtUtc,expiresAtUtc=x.ExpiresAtUtc}).ToListAsync(ct));}

    [HttpGet("rematches/outgoing")]
    public async Task<IActionResult> Outgoing(CancellationToken ct){var me=CurrentUserId;if(me==Guid.Empty)return Unauthorized();await Expire(ct);return Ok(await db.GameRematchRequests.AsNoTracking().Where(x=>x.RequesterUserId==me).OrderByDescending(x=>x.CreatedAtUtc).Take(100).Select(x=>new{id=x.Id,originalGameId=x.OriginalGameId,opponentUserId=x.OpponentUserId,status=x.Status,rematchGameId=x.RematchGameId,createdAtUtc=x.CreatedAtUtc,expiresAtUtc=x.ExpiresAtUtc}).ToListAsync(ct));}

    [HttpPost("rematches/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id,CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();
        var request = await db.GameRematchRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return NotFound();
        if (request.OpponentUserId != me) return Forbid();
        if (request.Status != "Pending") return Conflict(new { code = "rematch_not_pending" });

        var now = DateTime.UtcNow;
        if (request.ExpiresAtUtc <= now)
        {
            await db.GameRematchRequests
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc <= now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.RespondedAtUtc, now), ct);
            return Conflict(new { code = "rematch_not_pending" });
        }

        var original = await db.Games.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.OriginalGameId, ct);
        if (original is null) return NotFound(new { code = "original_game_not_found" });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservedAt = DateTime.UtcNow;
        var reserved = await db.GameRematchRequests
            .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > reservedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Accepting"), ct);
        if (reserved != 1)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "rematch_not_pending" });
        }

        try
        {
            var newId = await games.CreateGameAsync(
                new CreateGameRequest(original.BlackPlayerId, original.WhitePlayerId,
                    request.InitialTimeSeconds, request.IncrementSeconds, request.Rated), ct);
            var completedAt = DateTime.UtcNow;
            var completed = await db.GameRematchRequests
                .Where(x => x.Id == id && x.Status == "Accepting")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Accepted")
                    .SetProperty(x => x.RematchGameId, newId)
                    .SetProperty(x => x.RespondedAtUtc, completedAt), ct);
            if (completed != 1)
                throw new InvalidOperationException("The rematch reservation was lost before completion.");

            await tx.CommitAsync(ct);
            return Ok(new { id, status = "Accepted", originalGameId = request.OriginalGameId, rematchGameId = newId });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    [HttpPost("rematches/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();
        var x = await db.GameRematchRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x is null) return NotFound();
        if (x.OpponentUserId != me) return Forbid();
        if (x.Status != "Pending") return Conflict(new { code = "rematch_not_pending" });
        var respondedAt = DateTime.UtcNow;
        if (x.ExpiresAtUtc <= respondedAt)
        {
            await db.GameRematchRequests
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc <= respondedAt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
            return Conflict(new { code = "rematch_not_pending" });
        }
        var updated = await db.GameRematchRequests
            .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > respondedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Declined")
                .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
        return updated == 1
            ? Ok(new { id, status = "Declined", respondedAtUtc = respondedAt })
            : Conflict(new { code = "rematch_not_pending" });
    }

    [HttpDelete("rematches/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();
        var x = await db.GameRematchRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x is null) return NotFound();
        if (x.RequesterUserId != me) return Forbid();
        if (x.Status != "Pending") return Conflict(new { code = "rematch_not_pending" });
        var respondedAt = DateTime.UtcNow;
        if (x.ExpiresAtUtc <= respondedAt)
        {
            await db.GameRematchRequests
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc <= respondedAt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
            return Conflict(new { code = "rematch_not_pending" });
        }
        var updated = await db.GameRematchRequests
            .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > respondedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Cancelled")
                .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
        return updated == 1 ? NoContent() : Conflict(new { code = "rematch_not_pending" });
    }

    private Task<int> Expire(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.GameRematchRequests
            .Where(x => x.Status == "Pending" && x.ExpiresAtUtc <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Expired")
                .SetProperty(x => x.RespondedAtUtc, now), ct);
    }
}
