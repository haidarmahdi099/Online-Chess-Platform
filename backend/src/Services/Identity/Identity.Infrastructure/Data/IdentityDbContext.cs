using Identity.Domain.Entities;
using Identity.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Data;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens =>
    Set<RefreshToken>();
    public DbSet<LoginAttempt> LoginAttempts =>
    Set<LoginAttempt>();
    public DbSet<PasswordResetToken> PasswordResetTokens =>
    Set<PasswordResetToken>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new UserConfiguration());

        modelBuilder.ApplyConfiguration(
            new RefreshTokenConfiguration());

        modelBuilder.ApplyConfiguration(
            new LoginAttemptConfiguration());

        modelBuilder.ApplyConfiguration(
         new PasswordResetTokenConfiguration());

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.ToTable("FriendRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.SenderUserId, x.ReceiverUserId, x.Status });
            entity.HasIndex(x => new { x.ReceiverUserId, x.Status });
        });
        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.ToTable("UserBlocks");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BlockerUserId, x.BlockedUserId }).IsUnique();
        });
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.ToTable("UserPreferences");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.BoardTheme).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PieceTheme).HasMaxLength(40).IsRequired();
        });
    }
}