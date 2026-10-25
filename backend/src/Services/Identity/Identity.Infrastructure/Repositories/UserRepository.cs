using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return Array.Empty<User>();

        var idArray = ids.ToArray();

        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => idArray.Contains(user.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(
    string username,
    CancellationToken cancellationToken = default)
    {
        // Usernames are unique ignoring case (see the IX_Users_Username_Lower index).
        var normalized = username.ToLowerInvariant();

        return await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Username.ToLower() == normalized,
                cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);
    }

    public async Task<bool> ExistsByUsernameAsync(
    string username,
    CancellationToken cancellationToken = default)
    {
        var normalized = username.ToLowerInvariant();

        return await _dbContext.Users
            .AnyAsync(
                user => user.Username.ToLower() == normalized,
                cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AnyAsync(
                user => user.Email == email,
                cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                           pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _dbContext.Entry(user).State = EntityState.Detached;
            var normalizedUsername = user.Username.ToLowerInvariant();
            if (await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken))
                throw new Identity.Application.Common.ApplicationException("username_taken", "Username is already taken.");
            if (await _dbContext.Users.AnyAsync(u => u.Email == user.Email, cancellationToken))
                throw new Identity.Application.Common.ApplicationException("email_taken", "Email is already registered.");
            throw;
        }
    }

    public async Task UpdateAsync(
    User user,
    CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}