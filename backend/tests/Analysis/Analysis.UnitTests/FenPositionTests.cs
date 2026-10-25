using Analysis.Domain.Chess;

namespace Analysis.UnitTests;

public sealed class FenPositionTests
{
    private const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void TryCreate_accepts_and_normalizes_valid_fen()
    {
        Assert.True(FenPosition.TryCreate($"  {InitialFen}  ", out var position, out var error));
        Assert.Null(error);
        Assert.NotNull(position);
        Assert.Equal(InitialFen, position.Fen);
        Assert.True(position.IsWhiteToMove);
    }

    [Theory]
    [InlineData(null, "FEN is required.")]
    [InlineData("  ", "FEN is required.")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1", "Each side must have exactly one king.")]
    [InlineData("8/8/8/8/8/8/8/4K2k w - - 0", "FEN must contain six space-separated fields.")]
    [InlineData("8/8/8/8/8/8/8/4K2k x - - 0 1", "FEN side to move must be 'w' or 'b'.")]
    [InlineData("8/8/8/8/8/8/8/P3K2k w - - 0 1", "Pawns cannot stand on the first or last rank.")]
    public void TryCreate_rejects_invalid_fen(string? fen, string expectedError)
    {
        Assert.False(FenPosition.TryCreate(fen, out var position, out var error));
        Assert.Null(position);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void TryCreate_rejects_en_passant_inconsistent_with_side_to_move()
    {
        const string fen = "4k3/8/8/8/8/8/8/4K3 w - e3 0 1";

        Assert.False(FenPosition.TryCreate(fen, out _, out var error));
        Assert.Equal("FEN en passant square is inconsistent with the side to move.", error);
    }

    [Fact]
    public void TryCreate_rejects_fen_longer_than_supported_limit()
    {
        var fen = InitialFen + new string(' ', 100) + "x";

        Assert.False(FenPosition.TryCreate(fen, out _, out var error));
        Assert.Equal("FEN is too long.", error);
    }
}
