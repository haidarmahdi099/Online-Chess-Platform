using Identity.Domain.Entities;

namespace Identity.UnitTests.Domain;

public sealed class UserTests
{
    [Fact]
    public void VerifyEmail_WithMatchingUnexpiredToken_VerifiesAndConsumesToken()
    {
        var user = User.Create("Alice", "alice@example.com", "hash");
        user.SetEmailVerification("token-hash", DateTime.UtcNow.AddHours(1));

        Assert.True(user.VerifyEmail("token-hash"));
        Assert.True(user.EmailVerified);
        Assert.False(user.VerifyEmail("token-hash"));
    }

    [Fact]
    public void VerifyEmail_WithExpiredOrWrongToken_DoesNotVerify()
    {
        var user = User.Create("Alice", "alice@example.com", "hash");
        user.SetEmailVerification("token-hash", DateTime.UtcNow.AddSeconds(-1));

        Assert.False(user.VerifyEmail("token-hash"));
        Assert.False(user.EmailVerified);
    }

    [Fact]
    public void RecordFailedLogin_FifthFailureStartsFiveMinuteLockout()
    {
        var user = User.Create("Alice", "alice@example.com", "hash");

        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        Assert.True(user.IsLockedOut());
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Equal(1, user.LockoutCount);
        Assert.InRange(user.GetRemainingLockoutTime(), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ClearLoginLockout_ClearsFailureCounterAndLockoutHistory()
    {
        var user = User.Create("Alice", "alice@example.com", "hash");
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.ClearLoginLockout();

        Assert.False(user.IsLockedOut());
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Equal(0, user.LockoutCount);
    }

    [Fact]
    public void SetRole_RejectsUnknownRole()
    {
        var user = User.Create("Alice", "alice@example.com", "hash");

        Assert.Throws<ArgumentException>(() => user.SetRole("SuperUser"));
    }
}
