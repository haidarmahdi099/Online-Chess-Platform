namespace Game.Domain.Chess;

public sealed class ChessPosition
{
    public string Fen { get; }

    private ChessPosition(string fen)
    {
        Fen = fen;
    }

    public static ChessPosition FromFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            throw new ArgumentException(
                "FEN is required.",
                nameof(fen));
        }

        return new ChessPosition(fen.Trim());
    }

    public bool IsWhiteToMove()
    {
        var parts = Fen.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            throw new InvalidOperationException(
                "Invalid FEN position.");
        }

        return parts[1] == "w";
    }

    public bool IsBlackToMove()
    {
        return !IsWhiteToMove();
    }
}