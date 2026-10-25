using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task InvalidateActiveTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> TryCompleteResetAsync(
        User user,
        Guid tokenId,
        CancellationToken cancellationToken = default);
}
