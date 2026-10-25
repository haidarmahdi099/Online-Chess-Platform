namespace Game.Domain.ValueObjects;

public sealed record TimeControl(
    int InitialSeconds,
    int IncrementSeconds)
{
    public TimeSpan InitialTime =>
        TimeSpan.FromSeconds(InitialSeconds);

    public TimeSpan Increment =>
        TimeSpan.FromSeconds(IncrementSeconds);

    public static TimeControl Create(
        int initialSeconds,
        int incrementSeconds)
    {
        if (initialSeconds <= 0)
        {
            throw new ArgumentException(
                "Initial time must be greater than zero.",
                nameof(initialSeconds));
        }

        if (incrementSeconds < 0)
        {
            throw new ArgumentException(
                "Increment cannot be negative.",
                nameof(incrementSeconds));
        }

        return new TimeControl(
            initialSeconds,
            incrementSeconds);
    }
}