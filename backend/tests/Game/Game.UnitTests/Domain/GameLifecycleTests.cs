using Game.Domain.Entities;
using Game.Domain.Enums;
using Game.Domain.ValueObjects;
using ChessGame = Game.Domain.Entities.Game;

namespace Game.UnitTests.Domain;

public sealed class GameLifecycleTests
{
    private static readonly Guid White = Guid.NewGuid();
    private static readonly Guid Black = Guid.NewGuid();

    [Fact]
    public void Create_starts_waiting_with_initial_position_and_configured_clock()
    {
        var game = ChessGame.Create(White, Black, TimeControl.Create(300, 5), rated: true);

        Assert.Equal(GameStatus.Waiting, game.Status);
        Assert.Equal(ChessGame.InitialFen, game.Fen);
        Assert.Equal(300, game.InitialTimeSeconds);
        Assert.Equal(5, game.IncrementSeconds);
        Assert.True(game.Rated);
        Assert.Null(game.StartedAt);
    }

    [Fact]
    public void Create_rejects_empty_or_duplicate_players()
    {
        var control = TimeControl.Create(60, 0);

        Assert.Throws<ArgumentException>(() => ChessGame.Create(Guid.Empty, Black, control));
        Assert.Throws<ArgumentException>(() => ChessGame.Create(White, Guid.Empty, control));
        Assert.Throws<ArgumentException>(() => ChessGame.Create(White, White, control));
    }

    [Fact]
    public void Start_changes_waiting_game_to_live_once()
    {
        var game = NewGame();

        game.Start();

        Assert.Equal(GameStatus.Live, game.Status);
        Assert.NotNull(game.StartedAt);
        Assert.Throws<InvalidOperationException>(() => game.Start());
    }

    [Fact]
    public void Finish_requires_live_game_and_valid_result()
    {
        var waiting = NewGame();
        Assert.Throws<InvalidOperationException>(() => waiting.Finish(GameEndReason.Checkmate, White));

        var game = NewGame();
        game.Start();
        Assert.Throws<ArgumentException>(() => game.Finish(GameEndReason.Checkmate));
        Assert.Throws<ArgumentException>(() => game.Finish(GameEndReason.DrawAgreement, White));
        Assert.Throws<ArgumentException>(() => game.Finish(GameEndReason.Resignation, Guid.NewGuid()));
    }

    [Fact]
    public void Finish_records_winner_reason_and_finished_time()
    {
        var game = NewLiveGame();

        game.Finish(GameEndReason.Resignation, Black);

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(Black, game.WinnerId);
        Assert.Equal(GameEndReason.Resignation, game.EndReason);
        Assert.NotNull(game.FinishedAt);
    }

    [Fact]
    public void Draw_offer_is_available_to_opponent_and_clears_when_game_finishes()
    {
        var game = NewLiveGame();
        game.OfferDraw(White);

        Assert.True(game.HasDrawOfferFromOpponent(Black));
        Assert.False(game.HasDrawOfferFromOpponent(White));

        game.Finish(GameEndReason.DrawAgreement);

        Assert.Null(game.DrawOfferPlayerId);
    }

    [Fact]
    public void Draw_offer_rejects_non_player_and_non_live_game()
    {
        var waiting = NewGame();
        Assert.Throws<InvalidOperationException>(() => waiting.OfferDraw(White));

        var live = NewLiveGame();
        Assert.Throws<ArgumentException>(() => live.OfferDraw(Guid.NewGuid()));
    }

    [Fact]
    public void Turn_matches_initial_fen_and_updates_with_position()
    {
        var game = NewLiveGame();

        Assert.True(game.IsPlayerTurn(White));
        Assert.False(game.IsPlayerTurn(Black));
        Assert.False(game.IsPlayerTurn(Guid.NewGuid()));

        game.UpdatePosition("8/8/8/8/8/8/8/8 b - - 0 1");
        Assert.False(game.IsPlayerTurn(White));
        Assert.True(game.IsPlayerTurn(Black));
    }

    [Fact]
    public void Position_can_only_be_updated_while_live_and_must_not_be_blank()
    {
        var waiting = NewGame();
        Assert.Throws<InvalidOperationException>(() => waiting.UpdatePosition("fen"));

        var live = NewLiveGame();
        Assert.Throws<ArgumentException>(() => live.UpdatePosition(" "));
        live.UpdatePosition("  position  ");
        Assert.Equal("position", live.Fen);
    }

    [Fact]
    public void Spectator_access_waits_for_both_players_to_consent()
    {
        var game = NewGame();

        Assert.False(game.RespondToSpectatorConsent(White, true));
        Assert.False(game.SpectatorConsentResolved);
        Assert.False(game.IsPublic);

        Assert.True(game.RespondToSpectatorConsent(Black, true));
        Assert.True(game.SpectatorConsentResolved);
        Assert.False(game.IsPublic); // A waiting game is not public until it starts.
        game.Start();
        Assert.True(game.IsPublic);
    }

    [Fact]
    public void Spectator_decline_resolves_consent_and_prevents_public_access()
    {
        var game = NewGame();

        Assert.True(game.RespondToSpectatorConsent(White, false));
        Assert.True(game.SpectatorConsentRejected);
        Assert.False(game.IsPublic);
        Assert.Throws<InvalidOperationException>(() => game.RespondToSpectatorConsent(Black, true));
    }

    [Fact]
    public void Spectator_consent_is_player_only_and_cannot_change_after_game_ends()
    {
        var game = NewGame();
        Assert.Throws<UnauthorizedAccessException>(() => game.RespondToSpectatorConsent(Guid.NewGuid(), true));

        var live = NewLiveGame();
        live.Finish(GameEndReason.DrawAgreement);
        Assert.Throws<InvalidOperationException>(() => live.RespondToSpectatorConsent(White, true));
    }

    [Fact]
    public void Abort_waiting_game_does_not_create_a_result()
    {
        var game = NewGame();

        game.AbortWaitingGame();

        Assert.Equal(GameStatus.Aborted, game.Status);
        Assert.Null(game.WinnerId);
        Assert.Null(game.EndReason);
        Assert.NotNull(game.FinishedAt);
        Assert.Throws<InvalidOperationException>(() => game.AbortWaitingGame());
    }

    [Fact]
    public void Timeout_rejects_non_player_and_finishes_with_opponent_as_winner()
    {
        var game = NewLiveGame();
        Assert.Throws<ArgumentException>(() => game.FinishByTimeout(Guid.NewGuid()));

        game.FinishByTimeout(White);

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(Black, game.WinnerId);
        Assert.Equal(GameEndReason.Timeout, game.EndReason);
    }

    private static ChessGame NewGame() => ChessGame.Create(White, Black, TimeControl.Create(60, 0));

    private static ChessGame NewLiveGame()
    {
        var game = NewGame();
        game.Start();
        return game;
    }
}
