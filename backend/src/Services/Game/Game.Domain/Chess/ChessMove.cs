namespace Game.Domain.Chess;

public sealed record ChessMove(
    string From,
    string To,
    string? Promotion)
{
    public static ChessMove Create(
        string from,
        string to,
        string? promotion = null)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            throw new ArgumentException(
                "Origin square is required.",
                nameof(from));
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException(
                "Destination square is required.",
                nameof(to));
        }

        var normalizedFrom =
            from.Trim().ToLowerInvariant();

        var normalizedTo =
            to.Trim().ToLowerInvariant();

        ValidateSquare(
            normalizedFrom,
            nameof(from));

        ValidateSquare(
            normalizedTo,
            nameof(to));

        var normalizedPromotion =
            string.IsNullOrWhiteSpace(promotion)
                ? null
                : promotion.Trim().ToLowerInvariant();

        if (normalizedPromotion is not null &&
            normalizedPromotion is not ("q" or "r" or "b" or "n"))
        {
            throw new ArgumentException(
                "Promotion must be q, r, b, or n.",
                nameof(promotion));
        }

        return new ChessMove(
            normalizedFrom,
            normalizedTo,
            normalizedPromotion);
    }

    private static void ValidateSquare(
        string square,
        string parameterName)
    {
        if (square.Length != 2 ||
            square[0] < 'a' ||
            square[0] > 'h' ||
            square[1] < '1' ||
            square[1] > '8')
        {
            throw new ArgumentException(
                "Square must be in algebraic format such as e2 or e4.",
                parameterName);
        }
    }
}