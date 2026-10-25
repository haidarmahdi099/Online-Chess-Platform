using Identity.Domain.Repositories;

namespace Identity.Application.Auth.Logout;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var refreshTokenValue = (command.RefreshToken ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            throw new Identity.Application.Common.ApplicationException(
                "refresh_token_required",
                "Refresh token is required.");
        }

        var refreshToken =
            await _refreshTokenRepository.GetByTokenAsync(
                refreshTokenValue,
                cancellationToken);

        if (refreshToken is null)
        {
            // Don't reveal whether the token ever existed.
            return;
        }

        if (!refreshToken.IsActive())
        {
            return;
        }

        refreshToken.Revoke();

        await _refreshTokenRepository.UpdateAsync(
            refreshToken,
            cancellationToken);
    }
}