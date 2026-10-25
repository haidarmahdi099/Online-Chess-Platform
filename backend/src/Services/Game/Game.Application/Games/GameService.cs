
using Game.Application.Contracts;
using Game.Application.Events;
using Game.Domain.Chess;
using Game.Domain.Entities;
using Game.Domain.Enums;
using Game.Domain.ValueObjects;

namespace Game.Application.Games;

public sealed class GameService : IGameService
{
    // 100 halfmoves = 50 full moves by each side without a capture or pawn move.
    private const int FiftyMoveHalfmoves = 100;

    // Three occurrences need at least eight plies (two full knight shuffles each way).
    private const int MinimumPliesForRepetition = 8;

    private readonly IChessRules _chessRules;
    private readonly IGameRepository _gameRepository;

    public GameService(
        IChessRules chessRules,
        IGameRepository gameRepository)
    {
        _chessRules = chessRules;
        _gameRepository = gameRepository;
    }

    public async Task<MoveResult> MakeMoveAsync(
        Guid gameId,
        Guid playerId,
        MoveRequest request,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new MoveResult(
                false,
                "game_not_found",
                "Game was not found.",
                null,
                false,
                false,
                false);
        }

        if (!game.HasPlayer(playerId))
        {
            return new MoveResult(
                false,
                "not_a_player",
                "You are not a player in this game.",
                null,
                false,
                false,
                false);
        }

        if (game.Status != GameStatus.Live)
        {
            return new MoveResult(
                false,
                "game_not_live",
                "The game is not live.",
                null,
                false,
                false,
                false);
        }

        if (!game.IsPlayerTurn(playerId))
        {
            return new MoveResult(
                false,
                "not_your_turn",
                "It is not your turn.",
                null,
                false,
                false,
                false);
        }

        ChessMove chessMove;

        try
        {
            chessMove = ChessMove.Create(
                request.From,
                request.To,
                request.Promotion);
        }
        catch (ArgumentException ex)
        {
            return new MoveResult(
                false,
                "invalid_move_request",
                ex.Message,
                null,
                false,
                false,
                false);
        }

        var position = ChessPosition.FromFen(game.Fen);

        var clock = await _gameRepository.GetClockByGameIdAsync(
            gameId,
            cancellationToken);

        if (clock is null)
        {
            return new MoveResult(
                false,
                "clock_not_found",
                "Game clock was not found.",
                null,
                false,
                false,
                false);
        }

        if (clock.HasTimedOut())
        {
            // The player on move is the one whose clock expired.
            clock.Stop();

            game.FinishByTimeout(playerId);

            await QueueGameFinishedEventAsync(
                game,
                cancellationToken);

            await _gameRepository.SaveChangesAsync(
                cancellationToken);

            return new MoveResult(
                false,
                "timeout",
                "The player's clock has expired.",
                game.Fen,
                false,
                false,
                false,
                GameOver: true,
                EndReason: game.EndReason?.ToString());
        }

        var result = _chessRules.ValidateAndApply(
            position,
            chessMove);

        if (!result.IsLegal)
        {
            return new MoveResult(
                false,
                result.ErrorCode,
                result.ErrorMessage,
                null,
                result.IsCheck,
                result.IsCheckmate,
                result.IsStalemate);
        }

        // Recompute the halfmove/fullmove counters ourselves so the fifty-move rule never
        // depends on the chess library's bookkeeping.
        var previousFen = game.Fen;
        var newFen = ChessPositionAnalyzer.NormalizeClocks(
            previousFen,
            result.NewFen!,
            chessMove.From);

        // Moving declines an outstanding draw offer made by the opponent. An offer the mover
        // made themselves stays open for the opponent to answer.
        var drawOfferDeclined = game.HasDrawOfferFromOpponent(playerId);
        if (drawOfferDeclined)
        {
            game.ClearDrawOffer();
        }

        game.UpdatePosition(newFen);

        clock.CompleteTurn(
            TimeSpan.FromSeconds(game.IncrementSeconds));

        var persistedMoveCount = await _gameRepository.GetMoveCountAsync(
            gameId,
            cancellationToken);

        GameEndReason? endReason = null;
        Guid? decisiveWinnerId = null;

        if (result.IsCheckmate)
        {
            endReason = GameEndReason.Checkmate;
            decisiveWinnerId = playerId;
        }
        else if (result.IsStalemate)
        {
            endReason = GameEndReason.Stalemate;
        }
        else if (ChessPositionAnalyzer.IsInsufficientMaterial(newFen))
        {
            endReason = GameEndReason.InsufficientMaterial;
        }
        else if (ChessPositionAnalyzer.HalfmoveClock(newFen) >= FiftyMoveHalfmoves)
        {
            endReason = GameEndReason.FiftyMoveRule;
        }
        else if (persistedMoveCount + 1 >= MinimumPliesForRepetition &&
                 await IsThreefoldRepetitionAsync(gameId, newFen, cancellationToken))
        {
            endReason = GameEndReason.ThreefoldRepetition;
        }

        if (endReason.HasValue)
        {
            clock.Stop();

            game.Finish(
                endReason.Value,
                decisiveWinnerId);
        }

        var move = GameMove.Create(
            gameId,
            playerId,
            GameMove.CalculateMoveNumber(persistedMoveCount),
            chessMove.From,
            chessMove.To,
            chessMove.Promotion,
            newFen);

        await _gameRepository.AddMoveAsync(
            move,
            cancellationToken);

        // AddMoveAsync only tracks the move; it is not persisted until the following
        // SaveChangesAsync. Include this pending ply when deciding whether a short rated
        // game should count, otherwise a game ending on ply four is incorrectly aborted.
        await QueueGameFinishedEventAsync(
            game,
            cancellationToken,
            additionalPendingMoves: 1);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new MoveResult(
            true,
            null,
            null,
            newFen,
            result.IsCheck,
            result.IsCheckmate,
            result.IsStalemate,
            GameOver: game.Status != GameStatus.Live,
            EndReason: game.EndReason?.ToString(),
            DrawOfferDeclined: drawOfferDeclined);
    }

    private async Task<bool> IsThreefoldRepetitionAsync(
        Guid gameId,
        string newFen,
        CancellationToken cancellationToken)
    {
        var key = ChessPositionAnalyzer.PositionKey(newFen);

        // The initial position is never stored as a move, so count it explicitly. The new
        // position is not persisted yet, so it counts as one occurrence on its own.
        var occurrences = 1;
        if (ChessPositionAnalyzer.PositionKey(Game.Domain.Entities.Game.InitialFen) == key)
        {
            occurrences++;
        }

        var history = await _gameRepository.GetFenHistoryAsync(
            gameId,
            cancellationToken);

        foreach (var fen in history)
        {
            if (ChessPositionAnalyzer.PositionKey(fen) == key)
            {
                occurrences++;
            }
        }

        return occurrences >= 3;
    }

    public async Task<Guid> CreateTournamentGameAsync(Guid gameId, CreateGameRequest request, CancellationToken cancellationToken)
    {
        if (gameId == Guid.Empty) throw new ArgumentException("Game ID is required.", nameof(gameId));
        var existing = await _gameRepository.GetByIdAsync(gameId, cancellationToken);
        if (existing is not null)
        {
            if (existing.WhitePlayerId != request.WhitePlayerId || existing.BlackPlayerId != request.BlackPlayerId)
                throw new InvalidOperationException("The idempotency key is already associated with a different game.");
            return existing.Id;
        }
        var timeControl = TimeControl.Create(request.InitialTimeSeconds, request.IncrementSeconds);
        var game = Game.Domain.Entities.Game.CreateWithId(gameId, request.WhitePlayerId, request.BlackPlayerId, timeControl, rated: false);
        game.Start();
        var clock = GameClock.Create(game.Id, timeControl.InitialTime);
        clock.StartTurn();
        await _gameRepository.AddAsync(game, cancellationToken);
        await _gameRepository.AddClockAsync(clock, cancellationToken);
        await _gameRepository.SaveChangesAsync(cancellationToken);
        return game.Id;
    }

    public async Task<Guid> CreateGameAsync(
        CreateGameRequest request,
        CancellationToken cancellationToken)
    {
        var timeControl = TimeControl.Create(
            request.InitialTimeSeconds,
            request.IncrementSeconds);

        var game = Game.Domain.Entities.Game.Create(
            request.WhitePlayerId,
            request.BlackPlayerId,
            timeControl,
            request.Rated);

        var clock = GameClock.Create(
            game.Id,
            timeControl.InitialTime);

        await _gameRepository.AddAsync(
            game,
            cancellationToken);

        await _gameRepository.AddClockAsync(
            clock,
            cancellationToken);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return game.Id;
    }

    public async Task<GameActionResult> ResignAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new GameActionResult(
                false,
                "game_not_found",
                "The game was not found.");
        }

        if (!game.HasPlayer(playerId))
        {
            return new GameActionResult(
                false,
                "not_a_player",
                "You are not a player in this game.");
        }

        if (game.Status != GameStatus.Live)
        {
            return new GameActionResult(
                false,
                "game_not_live",
                "The game is not live.");
        }

        var clock = await _gameRepository.GetClockByGameIdAsync(
            gameId,
            cancellationToken);

        if (clock is null)
        {
            return new GameActionResult(
                false,
                "clock_not_found",
                "Game clock was not found.");
        }

        var winnerId = game.IsWhitePlayer(playerId)
            ? game.BlackPlayerId
            : game.WhitePlayerId;

        clock.Stop();

        game.Finish(
            GameEndReason.Resignation,
            winnerId);

        await QueueGameFinishedEventAsync(
            game,
            cancellationToken);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new GameActionResult(true);
    }

    public async Task<GameActionResult> AbortAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new GameActionResult(
                false,
                "game_not_found",
                "The game was not found.");
        }

        if (!game.HasPlayer(playerId))
        {
            return new GameActionResult(
                false,
                "not_a_player",
                "You are not a player in this game.");
        }

        if (game.Status != GameStatus.Live)
        {
            return new GameActionResult(
                false,
                "game_not_live",
                "The game is not live.");
        }

        // After both players have moved the game is real: use resign instead.
        var moveCount = await _gameRepository.GetMoveCountAsync(
            gameId,
            cancellationToken);

        if (moveCount >= 2)
        {
            return new GameActionResult(
                false,
                "abort_not_allowed",
                "A game can only be aborted before both players have moved. Resign instead.");
        }

        var clock = await _gameRepository.GetClockByGameIdAsync(
            gameId,
            cancellationToken);

        if (clock is null)
        {
            return new GameActionResult(
                false,
                "clock_not_found",
                "Game clock was not found.");
        }

        clock.Stop();

        game.AbortLiveGame();

        await _gameRepository.AddOutboxMessageAsync(
            GameOutboxMessages.CreateAborted(game, "AbortedByPlayer"),
            cancellationToken);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new GameActionResult(true);
    }

    public async Task<GameActionResult> OfferDrawAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new GameActionResult(
                false,
                "game_not_found",
                "The game was not found.");
        }

        if (!game.HasPlayer(playerId))
        {
            return new GameActionResult(
                false,
                "not_a_player",
                "You are not a player in this game.");
        }

        if (game.Status != GameStatus.Live)
        {
            return new GameActionResult(
                false,
                "game_not_live",
                "The game is not live.");
        }

        game.OfferDraw(playerId);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new GameActionResult(true);
    }

    public async Task<GameActionResult> AcceptDrawAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new GameActionResult(
                false,
                "game_not_found",
                "The game was not found.");
        }

        if (!game.HasPlayer(playerId))
        {
            return new GameActionResult(
                false,
                "not_a_player",
                "You are not a player in this game.");
        }

        if (game.Status != GameStatus.Live)
        {
            return new GameActionResult(
                false,
                "game_not_live",
                "The game is not live.");
        }

        if (game.DrawOfferPlayerId is null)
        {
            return new GameActionResult(
                false,
                "no_draw_offer",
                "There is no pending draw offer.");
        }

        if (!game.HasDrawOfferFromOpponent(playerId))
        {
            return new GameActionResult(
                false,
                "cannot_accept_own_offer",
                "You cannot accept your own draw offer.");
        }

        var clock = await _gameRepository.GetClockByGameIdAsync(
            gameId,
            cancellationToken);

        if (clock is null)
        {
            return new GameActionResult(
                false,
                "clock_not_found",
                "Game clock was not found.");
        }

        clock.Stop();

        game.Finish(GameEndReason.DrawAgreement);

        await QueueGameFinishedEventAsync(
            game,
            cancellationToken);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new GameActionResult(true);
    }

    public async Task<GameActionResult> DeclineDrawAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new GameActionResult(
                false,
                "game_not_found",
                "The game was not found.");
        }

        if (!game.HasPlayer(playerId))
        {
            return new GameActionResult(
                false,
                "not_a_player",
                "You are not a player in this game.");
        }

        if (game.Status != GameStatus.Live)
        {
            return new GameActionResult(
                false,
                "game_not_live",
                "The game is not live.");
        }

        if (game.DrawOfferPlayerId is null)
        {
            return new GameActionResult(
                false,
                "no_draw_offer",
                "There is no pending draw offer.");
        }

        if (!game.HasDrawOfferFromOpponent(playerId))
        {
            return new GameActionResult(
                false,
                "cannot_decline_own_offer",
                "You cannot decline your own draw offer.");
        }

        game.ClearDrawOffer();

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new GameActionResult(true);
    }

    public async Task<GameDetailsResponse?> GetGameAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return null;
        }

        if (!game.HasPlayer(playerId))
        {
            return null;
        }

        return MapGame(game);
    }

    public async Task<IReadOnlyList<GameDetailsResponse>> GetGamesAsync(
        Guid? playerId,
        bool liveOnly,
        CancellationToken cancellationToken)
    {
        var games = await _gameRepository.GetGamesAsync(
            playerId,
            liveOnly,
            cancellationToken);

        return games
            .Select(MapGame)
            .ToList();
    }

    public async Task<PagedResponse<GameDetailsResponse>> GetGamesPageAsync(
        Guid? playerId,
        bool liveOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _gameRepository.GetGamesPageAsync(
            playerId,
            liveOnly,
            page,
            pageSize,
            cancellationToken);

        var items = result.Items
            .Select(MapGame)
            .ToList();

        return new PagedResponse<GameDetailsResponse>(
            items,
            page,
            pageSize,
            result.TotalCount);
    }

    private static GameDetailsResponse MapGame(
        Game.Domain.Entities.Game game)
    {
        return new GameDetailsResponse(
            game.Id,
            game.WhitePlayerId,
            game.BlackPlayerId,
            game.Fen,
            game.Status.ToString(),
            game.WinnerId,
            game.EndReason?.ToString(),
            game.InitialTimeSeconds,
            game.IncrementSeconds,
            game.Rated,
            game.CreatedAt,
            game.StartedAt,
            game.FinishedAt);
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> GetChatAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return Array.Empty<ChatMessageResponse>();
        }

        var messages = await _gameRepository.GetChatMessagesAsync(
            gameId,
            cancellationToken);

        return messages
            .Select(message => new ChatMessageResponse(
                message.Id,
                message.UserId,
                message.Message,
                message.CreatedAt))
            .ToList();
    }

    public async Task<GameActionResult> SendChatMessageAsync(
        Guid gameId,
        Guid userId,
        ChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null)
        {
            return new GameActionResult(
                false,
                "game_not_found",
                "The game was not found.");
        }

        if (!game.HasPlayer(userId))
        {
            return new GameActionResult(
                false,
                "not_a_player",
                "You are not a player in this game.");
        }

        if (game.Status != GameStatus.Live)
        {
            return new GameActionResult(
                false,
                "game_not_live",
                "The game is not live.");
        }

        var message = GameChatMessage.Create(
            gameId,
            userId,
            request.Message);

        await _gameRepository.AddChatMessageAsync(
            message,
            cancellationToken);

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new GameActionResult(true);
    }

    public Task<GameStatsResponse> GetStatsAsync(
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return _gameRepository.GetStatsAsync(
            playerId,
            cancellationToken);
    }

    public async Task<GameClockResponse?> GetClockAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null || !game.HasPlayer(playerId))
        {
            return null;
        }

        var clock = await _gameRepository.GetClockByGameIdAsync(
            gameId,
            cancellationToken);

        if (clock is null)
        {
            return null;
        }

        return new GameClockResponse(
            gameId,
            (long)clock.GetWhiteRemaining().TotalMilliseconds,
            (long)clock.GetBlackRemaining().TotalMilliseconds,
            clock.WhiteToMove,
            clock.TurnStartedAt);
    }

    public async Task<GameMovesResponse?> GetMovesAsync(
        Guid gameId,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadViewableMovesAsync(gameId, viewerId, cancellationToken);

        if (loaded is null)
        {
            return null;
        }

        var (game, moves) = loaded.Value;

        return new GameMovesResponse(
            game.Id,
            Game.Domain.Entities.Game.InitialFen,
            game.Status.ToString(),
            moves);
    }

    public async Task<string?> GetPgnAsync(
        Guid gameId,
        Guid viewerId,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadViewableMovesAsync(gameId, viewerId, cancellationToken);

        if (loaded is null)
        {
            return null;
        }

        var (game, moves) = loaded.Value;

        return PgnWriter.Write(game, moves);
    }

    // Same visibility rule as the rest of the read API: the two players always, everyone else
    // only while the game is public (live and both players consented to spectators).
    private async Task<(Game.Domain.Entities.Game Game, IReadOnlyList<MoveHistoryItem> Moves)?>
        LoadViewableMovesAsync(
            Guid gameId,
            Guid viewerId,
            CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null || !(game.HasPlayer(viewerId) || game.IsPublic))
        {
            return null;
        }

        var persisted = await _gameRepository.GetMovesAsync(
            gameId,
            cancellationToken);

        var items = new List<MoveHistoryItem>(persisted.Count);
        var fenBefore = Game.Domain.Entities.Game.InitialFen;

        foreach (var move in persisted)
        {
            // MoveNumber is the 1-based ply: odd plies are White's.
            var ply = move.MoveNumber;

            items.Add(new MoveHistoryItem(
                ply,
                (ply + 1) / 2,
                ply % 2 == 1 ? "white" : "black",
                move.From,
                move.To,
                move.Promotion,
                SanNotation.Build(_chessRules, fenBefore, move.From, move.To, move.Promotion),
                move.FenAfterMove,
                move.CreatedAt));

            fenBefore = move.FenAfterMove;
        }

        return (game, items);
    }

    public async Task<GameDetailsResponse?> GetPublicGameAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null || !game.IsPublic)
        {
            return null;
        }

        return MapGame(game);
    }

    public async Task<SpectatorConsentResponse?> RespondToSpectatorConsentAsync(
        Guid gameId,
        Guid playerId,
        bool allowsSpectators,
        CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(
            gameId,
            cancellationToken);

        if (game is null || !game.HasPlayer(playerId))
        {
            return null;
        }

        // The second player answering after the first one declined (or after both agreed) is a
        // normal race, not an error: report the settled outcome instead of a 409.
        if (game.SpectatorConsentResolved)
        {
            return new SpectatorConsentResponse(
                GameStarted: false,
                ConsentResolved: true,
                SpectatorsAllowed: game.IsPublic,
                ConsentRejected: game.SpectatorConsentRejected);
        }

        var consentResolved = game.RespondToSpectatorConsent(
            playerId,
            allowsSpectators);

        if (consentResolved)
        {
            var clock = await _gameRepository.GetClockByGameIdAsync(
                gameId,
                cancellationToken);

            if (clock is null)
            {
                throw new InvalidOperationException(
                    "The game clock was not found.");
            }

            if (game.Status == GameStatus.Waiting)
            {
                game.Start();
                clock.StartTurn();
            }
        }

        await _gameRepository.SaveChangesAsync(
            cancellationToken);

        return new SpectatorConsentResponse(
            GameStarted: consentResolved,
            ConsentResolved: game.SpectatorConsentResolved,
            SpectatorsAllowed: game.IsPublic,
            ConsentRejected: game.SpectatorConsentRejected);
    }

    private async Task QueueGameFinishedEventAsync(
        Game.Domain.Entities.Game game,
        CancellationToken cancellationToken,
        int additionalPendingMoves = 0)
    {
        if (game.Status != GameStatus.Finished)
        {
            return;
        }

        var persistedMoveCount = await _gameRepository.GetMoveCountAsync(
            game.Id, cancellationToken);
        if (game.Rated && persistedMoveCount + additionalPendingMoves < 4)
        {
            // Too short to rate: the game is void. Tell the realtime side so clients do not
            // keep showing a decisive result that will never be rated.
            game.AbortShortRatedGame();

            await _gameRepository.AddOutboxMessageAsync(
                GameOutboxMessages.CreateAborted(game, "RatedGameTooShort"),
                cancellationToken);
            return;
        }

        await _gameRepository.AddOutboxMessageAsync(
            GameOutboxMessages.CreateFinished(game),
            cancellationToken);
    }
}
