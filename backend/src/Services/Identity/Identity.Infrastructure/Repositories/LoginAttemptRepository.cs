using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class LoginAttemptRepository
    : ILoginAttemptRepository
{
    private readonly IdentityDbContext _dbContext;

    public LoginAttemptRepository(
        IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LoginAttempt?> GetByIpAddressAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoginAttempts
            .FirstOrDefaultAsync(
                attempt => attempt.IpAddress == ipAddress,
                cancellationToken);
    }

    public async Task AddAsync(
        LoginAttempt loginAttempt,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.LoginAttempts.AddAsync(
            loginAttempt,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task UpdateAsync(
        LoginAttempt loginAttempt,
        CancellationToken cancellationToken = default)
    {
        _dbContext.LoginAttempts.Update(loginAttempt);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}