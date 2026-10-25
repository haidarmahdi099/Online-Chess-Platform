using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository
    : IPasswordResetTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public PasswordResetTokenRepository(
        IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PasswordResetTokens.AddAsync(
            token,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);
    }

    public async Task InvalidateActiveTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var activeTokens =
            await _dbContext.PasswordResetTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.UsedAt == null &&
                    token.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.MarkAsUsed();
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<bool> TryCompleteResetAsync(
        User user,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var consumed = await _dbContext.PasswordResetTokens
            .Where(token => token.Id == tokenId && token.UserId == user.Id &&
                            token.UsedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.UsedAt, now), cancellationToken);

        if (consumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
