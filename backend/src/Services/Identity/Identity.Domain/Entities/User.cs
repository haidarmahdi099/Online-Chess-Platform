namespace Identity.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }

    public string Username { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public bool EmailVerified { get; private set; }
    public string? EmailVerificationTokenHash { get; private set; }
    public DateTime? EmailVerificationExpiresAt { get; private set; }
    public int EmailVerificationFailedAttempts { get; private set; }
    public string? PendingEmail { get; private set; }
    public string? EmailChangeTokenHash { get; private set; }
    public DateTime? EmailChangeExpiresAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }

    public DateTime? LockoutUntil { get; private set; }

    public int LockoutCount { get; private set; }

    // Authorization role carried in the access token ("User" or "Admin").
    public string Role { get; private set; } = UserRoles.User;
    public ICollection<RefreshToken> RefreshTokens { get; private set; }
    = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens
    {
        get;
        private set;
    } = new List<PasswordResetToken>();


    private User()
    {
    }

    private User(
        Guid id,
        string username,
        string email,
        string passwordHash,
        DateTime createdAt)
    {
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public static User Create(
        string username,
        string email,
        string passwordHash)
    {
        return new User(
            Guid.NewGuid(),
            username,
            email,
            passwordHash,
            DateTime.UtcNow);
    }

    public bool IsLockedOut()
    {
        return LockoutUntil.HasValue &&
               LockoutUntil.Value > DateTime.UtcNow;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts < 5)
        {
            return;
        }

        LockoutCount++;

        var lockoutMinutes = LockoutCount switch
        {
            1 => 5,
            2 => 15,
            _ => 60
        };

        LockoutUntil = DateTime.UtcNow.AddMinutes(
            lockoutMinutes);

        FailedLoginAttempts = 0;
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockoutCount = 0;
    }

    public TimeSpan GetRemainingLockoutTime()
    {
        if (!IsLockedOut())
        {
            return TimeSpan.Zero;
        }

        return LockoutUntil!.Value - DateTime.UtcNow;
    }

    public void ClearLoginLockout()
    {
        FailedLoginAttempts = 0;
        LockoutCount = 0;
        LockoutUntil = null;
    }

    public bool SetRole(string role)
    {
        if (!UserRoles.IsValid(role))
        {
            throw new ArgumentException(
                "Unknown role.",
                nameof(role));
        }

        if (string.Equals(Role, role, StringComparison.Ordinal))
        {
            return false;
        }

        Role = role;
        return true;
    }

    public void SetEmailVerification(string tokenHash, DateTime expiresAt)
    {
        EmailVerificationTokenHash = tokenHash;
        EmailVerificationExpiresAt = expiresAt;
        EmailVerificationFailedAttempts = 0;
    }

    public bool VerifyEmail(string tokenHash)
    {
        if (EmailVerified || EmailVerificationTokenHash is null ||
            !EmailVerificationExpiresAt.HasValue || EmailVerificationExpiresAt.Value <= DateTime.UtcNow ||
            EmailVerificationFailedAttempts >= 5)
            return false;

        if (!string.Equals(EmailVerificationTokenHash, tokenHash, StringComparison.Ordinal))
        {
            EmailVerificationFailedAttempts++;
            if (EmailVerificationFailedAttempts >= 5)
            {
                EmailVerificationTokenHash = null;
                EmailVerificationExpiresAt = null;
            }
            return false;
        }

        EmailVerified = true;
        EmailVerificationTokenHash = null;
        EmailVerificationExpiresAt = null;
        EmailVerificationFailedAttempts = 0;
        return true;
    }

    public void SetPendingEmail(string email, string tokenHash, DateTime expiresAt)
    {
        PendingEmail = email;
        EmailChangeTokenHash = tokenHash;
        EmailChangeExpiresAt = expiresAt;
    }

    public bool ConfirmEmailChange(string tokenHash)
    {
        if (PendingEmail is null || EmailChangeTokenHash is null ||
            !EmailChangeExpiresAt.HasValue || EmailChangeExpiresAt.Value <= DateTime.UtcNow ||
            !string.Equals(EmailChangeTokenHash, tokenHash, StringComparison.Ordinal))
            return false;
        Email = PendingEmail;
        PendingEmail = null;
        EmailChangeTokenHash = null;
        EmailChangeExpiresAt = null;
        EmailVerified = true;
        return true;
    }

    public void ChangeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
        Username = username.Trim();
    }

    public void ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException(
                "Password hash is required.",
                nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }
}
