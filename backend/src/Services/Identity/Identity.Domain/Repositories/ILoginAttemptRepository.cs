using Identity.Domain.Entities;

namespace Identity.Domain.Repositories;

public interface ILoginAttemptRepository
{
    Task<LoginAttempt?> GetByIpAddressAsync(
        string ipAddress,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        LoginAttempt loginAttempt,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        LoginAttempt loginAttempt,
        CancellationToken cancellationToken = default);
}