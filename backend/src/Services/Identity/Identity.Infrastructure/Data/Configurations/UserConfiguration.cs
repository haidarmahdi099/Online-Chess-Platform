using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(
        EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(user => user.Username)
            .IsUnique();

        builder.Property(user => user.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .IsRequired();

        builder.Property(user => user.CreatedAt).IsRequired();
        // Keep the database default aligned with the domain default. Legacy rows are
        // preserved as verified by the migration that introduced this column.
        builder.Property(user => user.EmailVerified).HasDefaultValue(false).IsRequired();
        builder.Property(user => user.EmailVerificationTokenHash).HasMaxLength(64);
        builder.Property(user => user.EmailVerificationExpiresAt);
        builder.Property(user => user.EmailVerificationFailedAttempts).HasDefaultValue(0).IsRequired();
        builder.Property(user => user.PendingEmail).HasMaxLength(255);
        builder.Property(user => user.EmailChangeTokenHash).HasMaxLength(64);
        builder.Property(user => user.EmailChangeExpiresAt);

        builder.Property(user => user.FailedLoginAttempts)
           .IsRequired();

        builder.Property(user => user.LockoutUntil);

        builder.Property(user => user.LockoutCount)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(UserRoles.User);
    }
}
