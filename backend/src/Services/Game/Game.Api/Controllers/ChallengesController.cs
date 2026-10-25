using System.Net.Http.Json;
using System.Security.Claims;
using Game.Application.Games;
using Game.Domain.Entities;
using Game.Domain.Enums;
using Game.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Game.Api.Controllers;

[ApiController, Authorize, Route("challenges")]
public sealed class ChallengesController(GameDbContext db, IGameService games, IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
{
    public sealed record CreateChallengeRequest(Guid OpponentUserId, int InitialTimeSeconds, int IncrementSeconds, bool Rated = false);
    private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    [HttpPost]
    public async Task<IActionResult> Create(CreateChallengeRequest r, CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();
        if (r.OpponentUserId == Guid.Empty || r.OpponentUserId == me) return BadRequest(new { code = "invalid_opponent" });
        if (r.InitialTimeSeconds is < 1 or > 86400 || r.IncrementSeconds is < 0 or > 3600) return BadRequest(new { code = "invalid_time_control" });
        var eligibility = await CheckChallengeEligibility(me, r.OpponentUserId, ct);
        if (eligibility is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "identity_policy_unavailable" });
        if (!eligibility.Value.Allowed) return Conflict(new { code = eligibility.Value.Reason ?? "challenge_not_allowed" });
        if (await db.GameChallenges.AnyAsync(x => x.ChallengerUserId == me && x.OpponentUserId == r.OpponentUserId && x.Status == "Pending" && x.ExpiresAtUtc > DateTime.UtcNow, ct))
            return Conflict(new { code = "challenge_already_pending" });
        var entity = new GameChallenge { Id = Guid.NewGuid(), ChallengerUserId = me, OpponentUserId = r.OpponentUserId, InitialTimeSeconds = r.InitialTimeSeconds, IncrementSeconds = r.IncrementSeconds, Rated = r.Rated, Status = "Pending", CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddHours(24) };
        db.GameChallenges.Add(entity); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, ToDto(entity));
    }

    [HttpGet("incoming")]
    public async Task<IActionResult> Incoming(CancellationToken ct) { var me = CurrentUserId; if (me == Guid.Empty) return Unauthorized(); await ExpireChallenges(ct); return Ok(await db.GameChallenges.AsNoTracking().Where(x => x.OpponentUserId == me && x.Status == "Pending").OrderByDescending(x => x.CreatedAtUtc).Select(x => new { id=x.Id, challengerUserId=x.ChallengerUserId, initialTimeSeconds=x.InitialTimeSeconds, incrementSeconds=x.IncrementSeconds, rated=x.Rated, status=x.Status, createdAtUtc=x.CreatedAtUtc, expiresAtUtc=x.ExpiresAtUtc }).ToListAsync(ct)); }
    [HttpGet("outgoing")]
    public async Task<IActionResult> Outgoing(CancellationToken ct) { var me = CurrentUserId; if (me == Guid.Empty) return Unauthorized(); await ExpireChallenges(ct); return Ok(await db.GameChallenges.AsNoTracking().Where(x => x.ChallengerUserId == me).OrderByDescending(x => x.CreatedAtUtc).Take(100).Select(x => new { id=x.Id, opponentUserId=x.OpponentUserId, initialTimeSeconds=x.InitialTimeSeconds, incrementSeconds=x.IncrementSeconds, rated=x.Rated, status=x.Status, gameId=x.GameId, createdAtUtc=x.CreatedAtUtc, expiresAtUtc=x.ExpiresAtUtc }).ToListAsync(ct)); }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var me=CurrentUserId; if(me==Guid.Empty)return Unauthorized(); var x=await db.GameChallenges.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct); if(x is null)return NotFound(); if(x.ChallengerUserId!=me&&x.OpponentUserId!=me)return Forbid(); return Ok(ToDto(x)); }
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();

        var challenge = await db.GameChallenges.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (challenge is null) return NotFound();
        if (challenge.OpponentUserId != me) return Forbid();
        if (challenge.Status != "Pending") return Conflict(new { code = "challenge_not_pending" });

        var now = DateTime.UtcNow;
        if (challenge.ExpiresAtUtc <= now)
        {
            await db.GameChallenges
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc <= now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.RespondedAtUtc, now), ct);
            return Conflict(new { code = "challenge_not_pending" });
        }

        var eligibility = await CheckChallengeEligibility(challenge.ChallengerUserId, challenge.OpponentUserId, ct);
        if (eligibility is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "identity_policy_unavailable" });
        if (!eligibility.Value.Allowed)
        {
            await db.GameChallenges
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Declined")
                    .SetProperty(x => x.RespondedAtUtc, DateTime.UtcNow), ct);
            return Conflict(new { code = eligibility.Value.Reason ?? "challenge_not_allowed" });
        }

        // Conditional update is the reservation: only one concurrent accept can change Pending.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservedAt = DateTime.UtcNow;
        var reserved = await db.GameChallenges
            .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > reservedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Accepting"), ct);
        if (reserved != 1)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "challenge_not_pending" });
        }

        try
        {
            var gameId = await games.CreateGameAsync(
                new CreateGameRequest(challenge.ChallengerUserId, challenge.OpponentUserId,
                    challenge.InitialTimeSeconds, challenge.IncrementSeconds, challenge.Rated), ct);
            var completedAt = DateTime.UtcNow;
            var completed = await db.GameChallenges
                .Where(x => x.Id == id && x.Status == "Accepting")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Accepted")
                    .SetProperty(x => x.GameId, gameId)
                    .SetProperty(x => x.RespondedAtUtc, completedAt), ct);
            if (completed != 1)
                throw new InvalidOperationException("The challenge reservation was lost before completion.");

            await tx.CommitAsync(ct);
            return Ok(new { id, status = "Accepted", gameId });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();
        var x = await db.GameChallenges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x is null) return NotFound();
        if (x.OpponentUserId != me) return Forbid();
        if (x.Status != "Pending") return Conflict(new { code = "challenge_not_pending" });
        var now = DateTime.UtcNow;
        if (x.ExpiresAtUtc <= now)
        {
            await db.GameChallenges
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc <= now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.RespondedAtUtc, now), ct);
            return Conflict(new { code = "challenge_not_pending" });
        }
        var respondedAt = now;
        var updated = await db.GameChallenges
            .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > respondedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Declined")
                .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
        return updated == 1
            ? Ok(new { id, status = "Declined", respondedAtUtc = respondedAt })
            : Conflict(new { code = "challenge_not_pending" });
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var me = CurrentUserId;
        if (me == Guid.Empty) return Unauthorized();
        var x = await db.GameChallenges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x is null) return NotFound();
        if (x.ChallengerUserId != me) return Forbid();
        if (x.Status != "Pending") return Conflict(new { code = "challenge_not_pending" });
        var respondedAt = DateTime.UtcNow;
        if (x.ExpiresAtUtc <= respondedAt)
        {
            await db.GameChallenges
                .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc <= respondedAt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
            return Conflict(new { code = "challenge_not_pending" });
        }
        var updated = await db.GameChallenges
            .Where(x => x.Id == id && x.Status == "Pending" && x.ExpiresAtUtc > respondedAt)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Cancelled")
                .SetProperty(x => x.RespondedAtUtc, respondedAt), ct);
        return updated == 1 ? NoContent() : Conflict(new { code = "challenge_not_pending" });
    }
    private async Task<(bool Allowed, string? Reason)?> CheckChallengeEligibility(Guid challengerId, Guid opponentId, CancellationToken ct)
    {
        var key = configuration["InternalServices:TournamentKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            var client = httpClientFactory.CreateClient("IdentityApi");
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/challenges/eligibility")
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { challengerUserId = challengerId, opponentUserId = opponentId })
            };
            request.Headers.Add("X-Internal-Service-Key", key);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<ChallengeEligibilityResponse>(cancellationToken: ct);
            return result is null ? null : (result.Allowed, result.Reason);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    private sealed record ChallengeEligibilityResponse(bool Allowed, string? Reason);

    private async Task ExpireChallenges(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.GameChallenges
            .Where(x => x.Status == "Pending" && x.ExpiresAtUtc <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Expired")
                .SetProperty(x => x.RespondedAtUtc, now), ct);
    }
    private static object ToDto(GameChallenge x)=>new{id=x.Id,challengerUserId=x.ChallengerUserId,opponentUserId=x.OpponentUserId,initialTimeSeconds=x.InitialTimeSeconds,incrementSeconds=x.IncrementSeconds,rated=x.Rated,status=x.Status,gameId=x.GameId,createdAtUtc=x.CreatedAtUtc,expiresAtUtc=x.ExpiresAtUtc,respondedAtUtc=x.RespondedAtUtc};
}
