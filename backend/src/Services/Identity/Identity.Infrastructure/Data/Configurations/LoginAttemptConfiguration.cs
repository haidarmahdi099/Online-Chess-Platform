using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public sealed class LoginAttemptConfiguration
    : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(
        EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LoginAttempts");

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.IpAddress)
            .HasMaxLength(45)
            .IsRequired();

        builder.HasIndex(attempt => attempt.IpAddress)
            .IsUnique();

        builder.Property(attempt => attempt.FailedAttempts)
            .IsRequired();

        builder.Property(attempt => attempt.LockoutUntil);

        builder.Property(attempt => attempt.LockoutCount)
            .IsRequired();
    }
}