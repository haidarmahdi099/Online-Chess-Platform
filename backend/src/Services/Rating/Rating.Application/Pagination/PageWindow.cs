namespace Rating.Application.Pagination;

/// <summary>Validates page inputs and calculates an offset without integer overflow.</summary>
public static class PageWindow
{
    public const int MaxPageSize = 100;

    public static bool TryGetOffset(int page, int pageSize, out int offset)
    {
        offset = 0;

        if (page < 1 || pageSize is < 1 or > MaxPageSize)
            return false;

        var offset64 = ((long)page - 1) * pageSize;

        // Keep the last possible 1-based leaderboard rank representable as an int.
        if (offset64 + pageSize > int.MaxValue)
            return false;

        offset = (int)offset64;
        return true;
    }

    public static int GetOffset(int page, int pageSize)
    {
        if (!TryGetOffset(page, pageSize, out var offset))
            throw new ArgumentOutOfRangeException(
                nameof(page), "Page window is outside the supported range.");

        return offset;
    }
}
