namespace Game.Application.Validation;

public static class TimeControlValidator
{
    public static bool IsValid(
        int initialSeconds,
        int incrementSeconds,
        string? category)
    {
        if (initialSeconds <= 0 || incrementSeconds < 0)
            return false;

        if (string.IsNullOrWhiteSpace(category))
            return false;

        var estimatedDuration =
            (long)initialSeconds + 40L * incrementSeconds;

        return category.ToLowerInvariant() switch
        {
            "bullet" => estimatedDuration < 180,
            "blitz" => estimatedDuration >= 180 &&
                       estimatedDuration < 600,
            "rapid" => estimatedDuration >= 600 &&
                       estimatedDuration < 1800,
            "classical" => estimatedDuration >= 1800,
            _ => false
        };
    }
}