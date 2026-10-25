using Identity.Api.Auth.Login;
using Identity.Api.Auth.Logout;
using Identity.Api.Auth.Refresh;
using Identity.Api.Auth.Register;
using Identity.Application.Auth.ForgotPassword;
using Identity.Application.Auth.Login;
using Identity.Application.Auth.Logout;
using Identity.Application.Auth.Refresh;
using Identity.Application.Auth.Register;
using Identity.Application.Auth.VerifyEmail;
using Identity.Application.Auth.ResetPassword;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Infrastructure.Data;
using Identity.Application.Abstractions;
using Identity.Domain.Repositories;
using Identity.Application.Common;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Auth;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegisterUserHandler _registerUserHandler;
    private readonly LoginUserHandler _loginUserHandler;
    private readonly RefreshTokenHandler _refreshTokenHandler;
    private readonly LogoutHandler _logoutHandler;
    private readonly IdentityDbContext _db;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public AuthController(RegisterUserHandler registerUserHandler,
                          LoginUserHandler loginUserHandler,
                          RefreshTokenHandler refreshTokenHandler, LogoutHandler logoutHandler,
                          IdentityDbContext db, IUserRepository users, IPasswordHasher hasher,
                          IRefreshTokenRepository refreshTokens, IEmailSender emailSender,
                          IConfiguration configuration)
    {
        _registerUserHandler = registerUserHandler;
        _loginUserHandler = loginUserHandler;
        _refreshTokenHandler = refreshTokenHandler;
        _logoutHandler = logoutHandler;
        _db = db; _users = users; _hasher = hasher; _refreshTokens = refreshTokens; _emailSender = emailSender;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request,
                                              CancellationToken cancellationToken)
    {
        try
        {
            var command =
                new RegisterUserCommand(request.Username, request.Email, request.Password);

            var result = await _registerUserHandler.HandleAsync(command, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request,
                                           CancellationToken cancellationToken)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return BadRequest(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Request failed",
                        Detail = "Unable to determine client IP address.",
                        Extensions = { ["code"] = "ip_address_unavailable" }
                    });
            }

            var command =
                new LoginUserCommand(request.UsernameOrEmail, request.Password, ipAddress);

            var result = await _loginUserHandler.HandleAsync(command, cancellationToken);

            return Ok(result);
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

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request,
                                             CancellationToken cancellationToken)
    {
        try
        {
            var command = new RefreshTokenCommand(request.RefreshToken);

            var result = await _refreshTokenHandler.HandleAsync(command, cancellationToken);

            return Ok(result);
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

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request,
                                            CancellationToken cancellationToken)
    {
        try
        {
            var command = new LogoutCommand(request.RefreshToken);

            await _logoutHandler.HandleAsync(command, cancellationToken);

            return NoContent();
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

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command,
                                                    ForgotPasswordHandler handler,
                                                    CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command,
                                                   ResetPasswordHandler handler,
                                                   CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(command, cancellationToken);

            return NoContent();
        }
        catch (Identity.Application.Common.ApplicationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password reset failed",
                Detail = exception.Message,
                Extensions = { ["code"] = exception.Code }
            });
        }
    }

    private Guid? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    [AllowAnonymous, HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == (request.Email ?? string.Empty).Trim().ToLowerInvariant(), ct);
        if (user is null) return BadRequest(new { code = "invalid_verification_code", message = "The verification code is invalid, expired, or locked. Request a new code and try again." });
        var verified = user.VerifyEmail(EmailVerificationCode.Hash(
            user.Email,
            (request.Code ?? string.Empty).Trim(),
            _configuration["Jwt:SecretKey"] ?? string.Empty));
        await _db.SaveChangesAsync(ct);
        if (!verified) return BadRequest(new { code = "invalid_verification_code", message = "The verification code is invalid, expired, or locked. Request a new code and try again." });
        return NoContent();
    }

    [AllowAnonymous, HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
        // Avoid disclosing whether an account exists or is already verified.
        if (user is not null && !user.EmailVerified)
        {
            var code = EmailVerificationCode.Generate();
            user.SetEmailVerification(
                EmailVerificationCode.Hash(user.Email, code, _configuration["Jwt:SecretKey"] ?? string.Empty),
                DateTime.UtcNow.Add(EmailVerificationCode.Lifetime));
            await _db.SaveChangesAsync(ct);
            try { await _emailSender.SendEmailAsync(user.Email, "Your ChessPlatform verification code", EmailVerificationCode.CreateEmailHtml(code), ct); }
            catch (Exception ex) { HttpContext.RequestServices.GetRequiredService<ILogger<AuthController>>().LogError(ex, "Failed to resend verification email for user {UserId}.", user.Id); }
        }
        return NoContent();
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var id = CurrentUserId(); if (id is null) return Unauthorized();
        var user = await _users.GetByIdAsync(id.Value, ct); if (user is null) return NotFound();
        if (!_hasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash)) return BadRequest(new { code = "invalid_current_password", message = "Current password is incorrect." });
        try { PasswordValidator.Validate(request.NewPassword ?? string.Empty); } catch (Identity.Application.Common.ApplicationException ex) { return BadRequest(new { code = ex.Code, message = ex.Message }); }
        user.ChangePassword(_hasher.Hash(request.NewPassword ?? string.Empty)); await _users.UpdateAsync(user, ct); await _refreshTokens.RevokeAllForUserAsync(user.Id, ct); return NoContent();
    }

    [Authorize, HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request, CancellationToken ct)
    {
        var id = CurrentUserId(); if (id is null) return Unauthorized();
        var user = await _users.GetByIdAsync(id.Value, ct); if (user is null) return NotFound();
        if (!_hasher.Verify(request.Password ?? string.Empty, user.PasswordHash)) return BadRequest(new { code = "invalid_password", message = "Password is incorrect." });
        var email = (request.NewEmail ?? string.Empty).Trim().ToLowerInvariant();
        try { var parsed = new System.Net.Mail.MailAddress(email); if (parsed.Address != email || email.Length > 255) throw new FormatException(); } catch { return BadRequest(new { code = "email_invalid", message = "A valid email address is required." }); }
        if (await _users.ExistsByEmailAsync(email, ct)) return Conflict(new { code = "email_taken", message = "Email is already registered." });
        var token = NewToken(); user.SetPendingEmail(email, HashToken(token), DateTime.UtcNow.AddHours(24)); await _users.UpdateAsync(user, ct);
        await _emailSender.SendEmailAsync(email, "Confirm your ChessPlatform email change", $"<p>Confirmation token: {System.Net.WebUtility.HtmlEncode(token)}</p><p>Use POST /auth/confirm-email-change with this token.</p>", ct);
        return Accepted(new { message = "Confirmation email sent." });
    }

    [AllowAnonymous, HttpPost("confirm-email-change")]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] TokenRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.EmailChangeTokenHash == HashToken(request.Token ?? string.Empty), ct);
        if (user is null || !user.ConfirmEmailChange(HashToken(request.Token ?? string.Empty))) return BadRequest(new { code = "invalid_email_change_token", message = "The email change token is invalid or expired." });
        if (await _db.Users.AnyAsync(x => x.Id != user.Id && x.Email == user.Email, ct)) return Conflict(new { code = "email_taken", message = "Email is already registered." });
        await _db.SaveChangesAsync(ct); await _refreshTokens.RevokeAllForUserAsync(user.Id, ct); return NoContent();
    }

    [Authorize, HttpPost("logout-everywhere")]
    public async Task<IActionResult> LogoutEverywhere(CancellationToken ct)
    {
        var id = CurrentUserId(); if (id is null) return Unauthorized();
        await _refreshTokens.RevokeAllForUserAsync(id.Value, ct); return NoContent();
    }

    public sealed record VerifyEmailRequest(string Email, string Code);
    public sealed record ResendVerificationRequest(string Email);
    public sealed record TokenRequest(string Token);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public sealed record ChangeEmailRequest(string NewEmail, string Password);
}
