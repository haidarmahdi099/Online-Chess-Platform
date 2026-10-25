using Identity.Domain.Entities;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Admin;

/// <summary>
/// Keeps the Admin role in sync with configuration (Identity:AdminUsernames).
/// Listed users that already exist are promoted; Admins no longer listed are
/// demoted, so configuration is the single source of truth. Accounts are never
/// created here, so the listed accounts must register first. Runs at startup
/// and retries until the database (and the Role column) is available.
/// </summary>
public sealed class AdminRoleSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdminRoleSeeder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var adminUsernames = (configuration.GetSection("Identity:AdminUsernames").Get<string[]>() ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .ToHashSet();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(adminUsernames, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Admin role sync failed (is the database migrated?). Retrying in 30 seconds.");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task SyncAsync(HashSet<string> adminUsernames, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var configuredNames = adminUsernames.ToList();
        var users = await dbContext.Users
            .Where(u => u.Role == UserRoles.Admin || configuredNames.Contains(u.Username.ToLower()))
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var user in users)
        {
            var desired = adminUsernames.Contains(user.Username.ToLowerInvariant())
                ? UserRoles.Admin
                : UserRoles.User;
            if (user.SetRole(desired))
            {
                changed++;
                logger.LogInformation("User {Username} role set to {Role}.", user.Username, desired);
            }
        }

        if (changed > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            // Active refresh tokens re-issue access tokens from the stored role,
            // so the change reaches the user at the next refresh (at most 15 minutes).
        }

        var missing = adminUsernames.Count(name => users.All(u => !string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase)));
        if (missing > 0)
            logger.LogWarning("{Count} configured admin username(s) have no registered account yet; restart after they register.", missing);
    }
}
