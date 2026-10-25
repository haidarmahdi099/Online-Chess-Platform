using Identity.Application.Users.GetCurrentUser;
using Identity.Application.Users.GetUserByUsername;
using Identity.Application.Users.LookupUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Identity.Infrastructure.Data;
using Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Users;

[ApiController]
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private readonly GetCurrentUserHandler _getCurrentUserHandler;
    private readonly GetUserByUsernameHandler _getUserByUsernameHandler;
    private readonly LookupUsersHandler _lookupUsersHandler;
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;

    public UsersController(GetCurrentUserHandler getCurrentUserHandler,
                           GetUserByUsernameHandler getUserByUsernameHandler,
                           LookupUsersHandler lookupUsersHandler, IdentityDbContext db,
                           IPasswordHasher hasher)
    {
        _getCurrentUserHandler = getCurrentUserHandler;
        _getUserByUsernameHandler = getUserByUsernameHandler;
        _lookupUsersHandler = lookupUsersHandler;
        _db = db; _hasher = hasher;
    }

    /// <summary>
    /// Resolves user GUIDs (as returned by games, tournaments and the leaderboard) to usernames.
    /// Anonymous: only the public username is returned, never the email.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] LookupUsersRequest request,
                                            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _lookupUsersHandler.HandleAsync(request.UserIds, cancellationToken);

            return Ok(result.Users);
        }
        catch (Identity.Application.Common.ApplicationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = exception.Message,
                Extensions = { ["code"] = exception.Code }
            });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Invalid authentication",
                    Detail = "The access token does not contain a valid user ID.",
                    Extensions = { ["code"] = "invalid_user_claim" }
                });
        }

        try
        {
            var result = await _getCurrentUserHandler.HandleAsync(userId, cancellationToken);

            return Ok(result);
        }
        catch (Identity.Application.Common.ApplicationException exception)
        {
            if (exception.Code == "user_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = exception.Message,
                    Extensions = { ["code"] = exception.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = exception.Message,
                Extensions = { ["code"] = exception.Code }
            });
        }
    }
    [HttpGet("{username}")]
    public async Task<IActionResult> GetByUsername(string username,
                                                   CancellationToken cancellationToken)
    {
        try
        {
            var result = await _getUserByUsernameHandler.HandleAsync(username, cancellationToken);

            return Ok(result);
        }
        catch (Identity.Application.Common.ApplicationException exception)
        {
            if (exception.Code == "user_not_found")
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User not found",
                    Detail = exception.Message,
                    Extensions = { ["code"] = exception.Code }
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request failed",
                Detail = exception.Message,
                Extensions = { ["code"] = exception.Code }
            });
        }
    }


    public sealed record UpdateProfileRequest(string Username);

    [Authorize, HttpPut("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return Unauthorized();
        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length is < 3 or > 30 || !System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9_]+$"))
            return BadRequest(new { code = "invalid_username", message = "Username must be 3-30 characters and contain only letters, digits, or underscores." });
        var normalized = username.ToUpperInvariant();
        if (await _db.Users.AnyAsync(x => x.Id != userId && x.Username.ToUpper() == normalized, ct))
            return Conflict(new { code = "username_taken" });
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();
        user.ChangeUsername(username);
        await _db.SaveChangesAsync(ct);
        return Ok(new { userId = user.Id, username = user.Username, createdAt = user.CreatedAt });
    }

    [AllowAnonymous, HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2 || limit is < 1 or > 50)
            return BadRequest(new { code = "invalid_search", message = "q must contain at least 2 characters and limit must be 1-50." });
        var term = q.Trim();
        var users = await _db.Users.AsNoTracking().Where(x => EF.Functions.ILike(x.Username, $"%{term}%"))
            .OrderBy(x => x.Username).Take(limit).Select(x => new { userId = x.Id, username = x.Username, createdAt = x.CreatedAt }).ToListAsync(ct);
        return Ok(users);
    }

    public sealed record DeleteAccountRequest(string Password);

    [Authorize, HttpDelete("me")]
    public async Task<IActionResult> DeleteMe([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NoContent();
        if (!_hasher.Verify(request.Password, user.PasswordHash)) return BadRequest(new { code = "invalid_password", message = "Password is incorrect." });
        _db.FriendRequests.RemoveRange(await _db.FriendRequests
            .Where(x => x.SenderUserId == userId || x.ReceiverUserId == userId)
            .ToListAsync(ct));
        _db.UserBlocks.RemoveRange(await _db.UserBlocks
            .Where(x => x.BlockerUserId == userId || x.BlockedUserId == userId)
            .ToListAsync(ct));
        var preferences = await _db.UserPreferences.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (preferences is not null) _db.UserPreferences.Remove(preferences);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
