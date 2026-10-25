namespace Tournament.Application.Pagination;

/// <summary>Validates tournament list paging and calculates offsets without integer overflow.</summary>
public static class TournamentPageWindow
{
    public const int MaxPageSize = 100;

    public static bool TryGetOffset(int page, int pageSize, out int offset)
    {
        offset = 0;
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
            return false;

        var wideOffset = ((long)page - 1) * pageSize;
        if (wideOffset > int.MaxValue)
            return false;

        offset = (int)wideOffset;
        return true;
    }

    public static int GetOffset(int page, int pageSize) =>
        TryGetOffset(page, pageSize, out var offset)
            ? offset
            : throw new ArgumentOutOfRangeException(nameof(page), "The requested page window is invalid.");
}
