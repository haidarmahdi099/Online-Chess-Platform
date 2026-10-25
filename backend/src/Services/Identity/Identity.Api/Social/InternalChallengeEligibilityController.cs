using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Api.Social;

/// <summary>Service-to-service policy check. This controller is not intended to be exposed through the public gateway.</summary>
[ApiController]
[AllowAnonymous]
[Route("internal/challenges/eligibility")]
public sealed class InternalChallengeEligibilityController(IdentityDbContext db, IConfiguration configuration) : ControllerBase
{
    public sealed record EligibilityRequest(Guid ChallengerUserId, Guid OpponentUserId);

    [HttpPost]
    public async Task<IActionResult> Check([FromBody] EligibilityRequest request, CancellationToken ct)
    {
        var expected = configuration["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(expected)) expected = configuration["InternalServices:TournamentKey"];
        var supplied = Request.Headers["X-Internal-Service-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return Unauthorized();
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        if (expectedBytes.Length != suppliedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
            return Unauthorized();

        if (request.ChallengerUserId == Guid.Empty || request.OpponentUserId == Guid.Empty || request.ChallengerUserId == request.OpponentUserId)
            return BadRequest(new { code = "invalid_users" });

        var users = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.ChallengerUserId || u.Id == request.OpponentUserId)
            .Select(u => new { u.Id, u.EmailVerified })
            .ToListAsync(ct);
        if (users.Count != 2) return Ok(new { allowed = false, reason = "user_not_found" });
        if (users.Any(u => !u.EmailVerified)) return Ok(new { allowed = false, reason = "email_not_verified" });

        var blocked = await db.UserBlocks.AsNoTracking().AnyAsync(b =>
            (b.BlockerUserId == request.ChallengerUserId && b.BlockedUserId == request.OpponentUserId) ||
            (b.BlockerUserId == request.OpponentUserId && b.BlockedUserId == request.ChallengerUserId), ct);
        if (blocked) return Ok(new { allowed = false, reason = "blocked_relationship" });

        var preferences = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == request.OpponentUserId, ct);
        if (preferences is { AllowChallenges: false }) return Ok(new { allowed = false, reason = "challenges_disabled" });
        return Ok(new { allowed = true, reason = (string?)null });
    }
}
