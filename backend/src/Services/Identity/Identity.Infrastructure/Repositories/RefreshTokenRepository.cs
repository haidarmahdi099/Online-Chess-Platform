using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class RefreshTokenRepository
    : IRefreshTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public RefreshTokenRepository(
        IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(
            refreshToken,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<RefreshToken?> GetByTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                refreshToken => refreshToken.Token == RefreshToken.HashToken(token),
                cancellationToken);
    }

    public async Task UpdateAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        _dbContext.RefreshTokens.Update(refreshToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }


    public async Task<bool> RotateAsync(
        Guid oldTokenId,
        RefreshToken newToken,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var updated = await _dbContext.RefreshTokens
            .Where(token => token.Id == oldTokenId && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

        if (updated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await _dbContext.RefreshTokens.AddAsync(newToken, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllForUserAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
    {
        var tokens =
            await _dbContext.RefreshTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.RevokedAt == null)
                .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}