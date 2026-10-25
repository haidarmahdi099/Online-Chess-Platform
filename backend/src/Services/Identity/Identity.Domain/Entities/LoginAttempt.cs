namespace Identity.Domain.Entities;

public sealed class LoginAttempt
{
    public Guid Id { get; private set; }

    public string IpAddress { get; private set; } = null!;

    public int FailedAttempts { get; private set; }

    public DateTime? LockoutUntil { get; private set; }

    public int LockoutCount { get; private set; }

    private LoginAttempt()
    {
    }

    private LoginAttempt(
        Guid id,
        string ipAddress)
    {
        Id = id;
        IpAddress = ipAddress;
    }

    public static LoginAttempt Create(
        string ipAddress)
    {
        return new LoginAttempt(
            Guid.NewGuid(),
            ipAddress);
    }

    public bool IsLockedOut()
    {
        return LockoutUntil.HasValue &&
               LockoutUntil.Value > DateTime.UtcNow;
    }

    public void RecordFailedAttempt()
    {
        FailedAttempts++;

        if (FailedAttempts < 10)
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

        FailedAttempts = 0;
    }

    public void RecordSuccessfulAttempt()
    {
        FailedAttempts = 0;
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
}