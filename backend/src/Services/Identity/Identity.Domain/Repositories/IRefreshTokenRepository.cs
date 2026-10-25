using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default);

    Task<bool> RotateAsync(Guid oldTokenId, RefreshToken newToken, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(
    Guid userId,
    CancellationToken cancellationToken = default);
}