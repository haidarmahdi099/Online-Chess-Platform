using System.Security.Claims;
using Identity.Domain.Entities;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Social;

[ApiController]
[Authorize]
[Route("social")]
public sealed class SocialController(IdentityDbContext db) : ControllerBase
{
    public sealed record CreateFriendRequest(Guid UserId);
    public sealed record PreferencesRequest(string BoardTheme, string PieceTheme, bool SoundEnabled,
        bool PremovesEnabled, bool ShowOnlineStatus, bool AllowChallenges);

    private bool TryUserId(out Guid id) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out id);

    [HttpGet("friends")]
    public async Task<IActionResult> Friends(CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        var accepted = await db.FriendRequests.AsNoTracking()
            .Where(x => x.Status == "Accepted" && (x.SenderUserId == me || x.ReceiverUserId == me))
            .Select(x => new { FriendId = x.SenderUserId == me ? x.ReceiverUserId : x.SenderUserId })
            .ToListAsync(ct);
        var blocked = await db.UserBlocks.AsNoTracking().Where(x => x.BlockerUserId == me || x.BlockedUserId == me)
            .Select(x => x.BlockerUserId == me ? x.BlockedUserId : x.BlockerUserId).ToListAsync(ct);
        var ids = accepted.Select(x => x.FriendId).Distinct().Except(blocked).ToArray();
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { userId = x.Id, username = x.Username, createdAt = x.CreatedAt }).ToListAsync(ct);
        return Ok(users);
    }

    [HttpGet("friend-requests/incoming")]
    public async Task<IActionResult> Incoming(CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        var requests = await db.FriendRequests.AsNoTracking()
            .Where(x => x.ReceiverUserId == me && x.Status == "Pending")
            .Join(db.Users.AsNoTracking(), r => r.SenderUserId, u => u.Id,
                (r, u) => new { id = r.Id, userId = u.Id, username = u.Username, createdAt = r.CreatedAt })
            .OrderByDescending(x => x.createdAt).ToListAsync(ct);
        return Ok(requests);
    }

    [HttpGet("friend-requests/outgoing")]
    public async Task<IActionResult> Outgoing(CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        return Ok(await db.FriendRequests.AsNoTracking().Where(x => x.SenderUserId == me && x.Status == "Pending")
            .OrderByDescending(x => x.CreatedAt).Select(x => new { id = x.Id, userId = x.ReceiverUserId, createdAt = x.CreatedAt }).ToListAsync(ct));
    }

    [HttpPost("friend-requests")]
    public async Task<IActionResult> Send([FromBody] CreateFriendRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        if (request.UserId == Guid.Empty || request.UserId == me) return BadRequest(new { code = "invalid_target" });
        if (!await db.Users.AnyAsync(x => x.Id == request.UserId, ct)) return NotFound(new { code = "user_not_found" });
        if (await db.UserBlocks.AnyAsync(x => (x.BlockerUserId == me && x.BlockedUserId == request.UserId) || (x.BlockerUserId == request.UserId && x.BlockedUserId == me), ct))
            return Conflict(new { code = "blocked_relationship" });
        var existing = await db.FriendRequests.FirstOrDefaultAsync(x =>
            ((x.SenderUserId == me && x.ReceiverUserId == request.UserId) || (x.SenderUserId == request.UserId && x.ReceiverUserId == me)) &&
            (x.Status == "Pending" || x.Status == "Accepted"), ct);
        if (existing is not null) return Conflict(new { code = existing.Status == "Accepted" ? "already_friends" : "request_exists" });
        var entity = new FriendRequest { Id = Guid.NewGuid(), SenderUserId = me, ReceiverUserId = request.UserId, Status = "Pending", CreatedAt = DateTime.UtcNow };
        db.FriendRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        return Created($"/social/friend-requests/outgoing", new { id = entity.Id, userId = entity.ReceiverUserId, status = entity.Status, createdAt = entity.CreatedAt });
    }

    [HttpPost("friend-requests/{id:guid}/accept")]
    public Task<IActionResult> Accept(Guid id, CancellationToken ct) => Respond(id, true, ct);
    [HttpPost("friend-requests/{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, CancellationToken ct) => Respond(id, false, ct);

    private async Task<IActionResult> Respond(Guid id, bool accept, CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        var request = await db.FriendRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return NotFound();
        if (request.ReceiverUserId != me) return Forbid();
        if (request.Status != "Pending") return Conflict(new { code = "request_not_pending" });
        if (accept && await db.UserBlocks.AnyAsync(x =>
                (x.BlockerUserId == request.SenderUserId && x.BlockedUserId == request.ReceiverUserId) ||
                (x.BlockerUserId == request.ReceiverUserId && x.BlockedUserId == request.SenderUserId), ct))
            return Conflict(new { code = "blocked_relationship" });
        request.Status = accept ? "Accepted" : "Declined";
        request.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { id = request.Id, status = request.Status, respondedAt = request.RespondedAt });
    }

    [HttpDelete("friends/{userId:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid userId, CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        var rows = await db.FriendRequests.Where(x => x.Status == "Accepted" &&
            ((x.SenderUserId == me && x.ReceiverUserId == userId) || (x.SenderUserId == userId && x.ReceiverUserId == me))).ToListAsync(ct);
        if (rows.Count == 0) return NotFound();
        db.FriendRequests.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("blocks/{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        if (userId == me) return BadRequest(new { code = "cannot_block_self" });
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct)) return NotFound(new { code = "user_not_found" });
        if (!await db.UserBlocks.AnyAsync(x => x.BlockerUserId == me && x.BlockedUserId == userId, ct))
            db.UserBlocks.Add(new UserBlock { Id = Guid.NewGuid(), BlockerUserId = me, BlockedUserId = userId, CreatedAt = DateTime.UtcNow });
        var relationships = await db.FriendRequests.Where(x =>
            ((x.SenderUserId == me && x.ReceiverUserId == userId) || (x.SenderUserId == userId && x.ReceiverUserId == me))).ToListAsync(ct);
        db.FriendRequests.RemoveRange(relationships);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("blocks/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        var block = await db.UserBlocks.FirstOrDefaultAsync(x => x.BlockerUserId == me && x.BlockedUserId == userId, ct);
        if (block is null) return NotFound();
        db.UserBlocks.Remove(block); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("blocks")]
    public async Task<IActionResult> Blocks(CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        return Ok(await db.UserBlocks.AsNoTracking().Where(x => x.BlockerUserId == me)
            .Join(db.Users.AsNoTracking(), b => b.BlockedUserId, u => u.Id,
                (b, u) => new { userId = u.Id, username = u.Username, createdAt = b.CreatedAt }).ToListAsync(ct));
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        var p = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me, ct);
        if (p is null) return Ok(new { userId = me, boardTheme = "classic", pieceTheme = "standard", soundEnabled = true, premovesEnabled = true, showOnlineStatus = true, allowChallenges = true, updatedAt = (DateTime?)null });
        return Ok(new { userId = p.UserId, boardTheme = p.BoardTheme, pieceTheme = p.PieceTheme, soundEnabled = p.SoundEnabled, premovesEnabled = p.PremovesEnabled, showOnlineStatus = p.ShowOnlineStatus, allowChallenges = p.AllowChallenges, updatedAt = (DateTime?)p.UpdatedAt });
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> PutPreferences([FromBody] PreferencesRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var me)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.BoardTheme) || request.BoardTheme.Length > 40 || string.IsNullOrWhiteSpace(request.PieceTheme) || request.PieceTheme.Length > 40)
            return BadRequest(new { code = "invalid_preferences", message = "Theme names must contain 1 to 40 characters." });
        var p = await db.UserPreferences.FirstOrDefaultAsync(x => x.UserId == me, ct);
        if (p is null) { p = new UserPreference { UserId = me }; db.UserPreferences.Add(p); }
        p.BoardTheme = request.BoardTheme.Trim(); p.PieceTheme = request.PieceTheme.Trim();
        p.SoundEnabled = request.SoundEnabled; p.PremovesEnabled = request.PremovesEnabled;
        p.ShowOnlineStatus = request.ShowOnlineStatus; p.AllowChallenges = request.AllowChallenges; p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { userId = p.UserId, boardTheme = p.BoardTheme, pieceTheme = p.PieceTheme, soundEnabled = p.SoundEnabled, premovesEnabled = p.PremovesEnabled, showOnlineStatus = p.ShowOnlineStatus, allowChallenges = p.AllowChallenges, updatedAt = p.UpdatedAt });
    }
}
