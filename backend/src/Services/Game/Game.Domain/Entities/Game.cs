using Game.Domain.Chess;
using Game.Domain.Enums;
using Game.Domain.ValueObjects;

namespace Game.Domain.Entities;

public sealed class Game
{
    public const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public Guid Id { get; private set; }
    public uint Version { get; private set; }

    public Guid WhitePlayerId { get; private set; }

    public Guid BlackPlayerId { get; private set; }

    public string Fen { get; private set; } = null!;

    public GameStatus Status { get; private set; }

    public Guid? WinnerId { get; private set; }

    public GameEndReason? EndReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? FinishedAt { get; private set; }
    public int InitialTimeSeconds { get; private set; }

    public int IncrementSeconds { get; private set; }
    public bool Rated { get; private set; }
    public Guid? DrawOfferPlayerId { get; private set; }
    public bool WhiteSpectatorConsent { get; private set; }

    public bool BlackSpectatorConsent { get; private set; }

    public bool SpectatorConsentRejected { get; private set; }

    public bool IsPublic =>
        Status == GameStatus.Live &&
        WhiteSpectatorConsent &&
        BlackSpectatorConsent &&
        !SpectatorConsentRejected;

    public bool SpectatorConsentResolved =>
        SpectatorConsentRejected ||
        (WhiteSpectatorConsent && BlackSpectatorConsent);

    private Game()
    {
    }

    private Game(
    Guid id,
    Guid whitePlayerId,
    Guid blackPlayerId,
    string fen,
    TimeControl timeControl,
    bool rated)
    {
        Id = id;
        WhitePlayerId = whitePlayerId;
        BlackPlayerId = blackPlayerId;
        Fen = fen;
        Rated = rated;

        Status = GameStatus.Waiting;
        CreatedAt = DateTime.UtcNow;
        InitialTimeSeconds = timeControl.InitialSeconds;
        IncrementSeconds = timeControl.IncrementSeconds;
    }

    public static Game CreateWithId(
    Guid id, Guid whitePlayerId, Guid blackPlayerId, TimeControl timeControl, bool rated = false)
    {
        if (id == Guid.Empty) throw new ArgumentException("Game ID is required.", nameof(id));
        if (whitePlayerId == Guid.Empty) throw new ArgumentException("White player ID is required.", nameof(whitePlayerId));
        if (blackPlayerId == Guid.Empty) throw new ArgumentException("Black player ID is required.", nameof(blackPlayerId));
        if (whitePlayerId == blackPlayerId) throw new ArgumentException("A player cannot play against themselves.");
        return new Game(id, whitePlayerId, blackPlayerId, InitialFen, timeControl, rated);
    }

    public static Game Create(
    Guid whitePlayerId,
    Guid blackPlayerId,
    TimeControl timeControl,
    bool rated = false)
    {
        if (whitePlayerId == Guid.Empty)
            throw new ArgumentException(
                "White player ID is required.",
                nameof(whitePlayerId));

        if (blackPlayerId == Guid.Empty)
            throw new ArgumentException(
                "Black player ID is required.",
                nameof(blackPlayerId));

        if (whitePlayerId == blackPlayerId)
            throw new ArgumentException(
                "A player cannot play against themselves.");

        return new Game(
            Guid.NewGuid(),
            whitePlayerId,
            blackPlayerId,
            InitialFen,
            timeControl,
            rated);
    }

    public void Start()
    {
        if (Status != GameStatus.Waiting)
        {
            throw new InvalidOperationException(
                "Only a waiting game can be started.");
        }

        Status = GameStatus.Live;
        StartedAt = DateTime.UtcNow;
    }

    public void AbortShortRatedGame()
    {
        if (Status != GameStatus.Finished)
            throw new InvalidOperationException("Only a finished game can be marked aborted.");

        // An aborted game has no result: it must not count as a win, loss or draw anywhere.
        Status = GameStatus.Aborted;
        WinnerId = null;
        EndReason = null;
    }

    /// <summary>
    /// A player cancels a live game before it really started (see GameService.AbortAsync for the
    /// move limit). The game ends without a result and is never rated.
    /// </summary>
    public void AbortLiveGame()
    {
        if (Status != GameStatus.Live)
            throw new InvalidOperationException("Only a live game can be aborted.");

        Status = GameStatus.Aborted;
        WinnerId = null;
        EndReason = null;
        FinishedAt = DateTime.UtcNow;
        DrawOfferPlayerId = null;
    }

    public void AbortWaitingGame()
    {
        if (Status != GameStatus.Waiting)
            throw new InvalidOperationException("Only a waiting game can be aborted.");

        Status = GameStatus.Aborted;
        FinishedAt = DateTime.UtcNow;
    }

    public void Finish(
        GameEndReason endReason,
        Guid? winnerId = null)
    {
        if (Status != GameStatus.Live)
        {
            throw new InvalidOperationException(
                "Only a live game can be finished.");
        }

        if (winnerId.HasValue &&
            winnerId.Value != WhitePlayerId &&
            winnerId.Value != BlackPlayerId)
        {
            throw new ArgumentException(
                "Winner must be one of the game players.",
                nameof(winnerId));
        }

        if (IsDrawReason(endReason) &&
            winnerId.HasValue)
        {
            throw new ArgumentException(
                "A drawn game cannot have a winner.",
                nameof(winnerId));
        }

        if (!IsDrawReason(endReason) &&
            !winnerId.HasValue)
        {
            throw new ArgumentException(
                "A decisive game must have a winner.",
                nameof(winnerId));
        }

        Status = GameStatus.Finished;
        EndReason = endReason;
        WinnerId = winnerId;
        FinishedAt = DateTime.UtcNow;
        DrawOfferPlayerId = null;
    }

    /// <summary>
    /// Ends the game because <paramref name="timedOutPlayerId"/> ran out of time. If the opponent
    /// could never checkmate with the material left, the game is drawn instead of lost.
    /// </summary>
    public void FinishByTimeout(Guid timedOutPlayerId)
    {
        if (!HasPlayer(timedOutPlayerId))
        {
            throw new ArgumentException(
                "Only a game player can time out.",
                nameof(timedOutPlayerId));
        }

        var timedOutIsWhite = IsWhitePlayer(timedOutPlayerId);
        var opponentId = timedOutIsWhite ? BlackPlayerId : WhitePlayerId;

        if (ChessPositionAnalyzer.CannotWin(Fen, white: !timedOutIsWhite))
        {
            Finish(GameEndReason.InsufficientMaterial);
            return;
        }

        Finish(GameEndReason.Timeout, opponentId);
    }

    private static bool IsDrawReason(
        GameEndReason endReason)
    {
        return endReason is
            GameEndReason.DrawAgreement or
            GameEndReason.Stalemate or
            GameEndReason.ThreefoldRepetition or
            GameEndReason.FiftyMoveRule or
            GameEndReason.InsufficientMaterial;
    }

    public bool HasPlayer(Guid userId)
    {
        return userId == WhitePlayerId ||
               userId == BlackPlayerId;
    }

    public bool IsWhitePlayer(Guid userId)
    {
        return userId == WhitePlayerId;
    }

    public bool IsBlackPlayer(Guid userId)
    {
        return userId == BlackPlayerId;
    }

    public void UpdatePosition(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new ArgumentException(
                "FEN is required.",
                nameof(fen));

        if (Status != GameStatus.Live)
            throw new InvalidOperationException(
                "Only a live game can update its position.");

        Fen = fen.Trim();
    }

    public bool IsPlayerTurn(Guid userId)
    {
        if (!HasPlayer(userId))
            return false;

        var position = ChessPosition.FromFen(Fen);

        return position.IsWhiteToMove()
            ? IsWhitePlayer(userId)
            : IsBlackPlayer(userId);
    }

    public void OfferDraw(Guid playerId)
    {
        if (Status != GameStatus.Live)
        {
            throw new InvalidOperationException(
                "A draw can only be offered during a live game.");
        }

        if (!HasPlayer(playerId))
        {
            throw new ArgumentException(
                "Only a game player can offer a draw.",
                nameof(playerId));
        }

        DrawOfferPlayerId = playerId;
    }

    public bool HasDrawOfferFromOpponent(Guid playerId)
    {
        return DrawOfferPlayerId.HasValue &&
               DrawOfferPlayerId.Value != playerId;
    }

    public void ClearDrawOffer()
    {
        DrawOfferPlayerId = null;
    }


    public bool RespondToSpectatorConsent(
    Guid playerId,
    bool allowsSpectators)
    {
        if (!HasPlayer(playerId))
        {
            throw new UnauthorizedAccessException(
                "Only a game player can respond to spectator consent.");
        }

        if (Status is not (GameStatus.Waiting or GameStatus.Live))
        {
            throw new InvalidOperationException(
                "Spectator consent cannot be changed after the game has ended.");
        }

        if (SpectatorConsentResolved)
        {
            throw new InvalidOperationException(
                "Spectator consent has already been resolved.");
        }

        if (!allowsSpectators)
        {
            SpectatorConsentRejected = true;
            return true;
        }

        if (IsWhitePlayer(playerId))
            WhiteSpectatorConsent = true;
        else
            BlackSpectatorConsent = true;

        return SpectatorConsentResolved;
    }

}