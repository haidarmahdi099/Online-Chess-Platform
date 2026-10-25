using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;

namespace Identity.Application.Auth.Refresh;

public sealed class RefreshTokenHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public RefreshTokenHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        ITokenService tokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<RefreshTokenResult> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        var refreshTokenValue = (command.RefreshToken ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            throw new Identity.Application.Common.ApplicationException(
                "refresh_token_required",
                "Refresh token is required.");
        }

        var existingToken =
            await _refreshTokenRepository.GetByTokenAsync(
                refreshTokenValue,
                cancellationToken);

        if (existingToken is null)
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_refresh_token",
                "Refresh token is invalid or expired.");
        }

        if (!existingToken.IsActive())
        {
            if (existingToken.RevokedAt.HasValue)
                await _refreshTokenRepository.RevokeAllForUserAsync(existingToken.UserId, cancellationToken);
            throw new Identity.Application.Common.ApplicationException(
                "invalid_refresh_token",
                "Refresh token is invalid or expired.");
        }

        var user = await _userRepository.GetByIdAsync(
            existingToken.UserId,
            cancellationToken);

        if (user is null)
        {
            throw new Identity.Application.Common.ApplicationException(
                "invalid_refresh_token",
                "Refresh token is invalid or expired.");
        }

        var accessToken = _tokenService.CreateAccessToken(
            user.Id,
            user.Username,
            user.Role);

        var newRefreshTokenValue =
            _tokenService.CreateRefreshToken();

        var newRefreshToken = RefreshToken.Create(
            user.Id,
            newRefreshTokenValue,
            DateTime.UtcNow.AddDays(30));

        var rotated = await _refreshTokenRepository.RotateAsync(
            existingToken.Id, newRefreshToken, cancellationToken);
        if (!rotated)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(existingToken.UserId, cancellationToken);
            throw new Identity.Application.Common.ApplicationException(
                "invalid_refresh_token",
                "Refresh token is invalid or expired.");
        }

        return new RefreshTokenResult(
            accessToken,
            newRefreshTokenValue);
    }
}