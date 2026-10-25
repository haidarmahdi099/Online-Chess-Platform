using Game.Domain.Entities;
using Game.Domain.Enums;
using System.Text.Json;
using GameEntity = Game.Domain.Entities.Game;

namespace Game.Application.Events;

/// <summary>
/// Single place that turns a terminal game state into an outbox message, so the move path,
/// the clock watcher and every other path emit identical, complete events.
/// </summary>
public static class GameOutboxMessages
{
    public const string GameFinishedType = "game.finished";
    public const string GameAbortedType = "game.aborted";
    public const string GameMatchedType = "game.matched";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static OutboxMessage CreateFinished(GameEntity game)
    {
        if (game.Status != GameStatus.Finished)
            throw new InvalidOperationException("Only a finished game can produce a finished event.");

        var result = game.WinnerId switch
        {
            var winner when winner == game.WhitePlayerId => "WhiteWin",
            var winner when winner == game.BlackPlayerId => "BlackWin",
            null => "Draw",
            _ => throw new InvalidOperationException("The finished game has an invalid winner.")
        };

        // A game can only finish once; the game id is a stable event id, so redelivery and
        // timeout/move races can never apply the same result twice downstream.
        var finishedEvent = new GameFinishedEvent(
            EventId: game.Id,
            GameId: game.Id,
            WhitePlayerId: game.WhitePlayerId,
            BlackPlayerId: game.BlackPlayerId,
            Result: result,
            OccurredAtUtc: game.FinishedAt ?? DateTime.UtcNow,
            Rated: game.Rated,
            EndReason: game.EndReason?.ToString(),
            WinnerId: game.WinnerId,
            InitialTimeSeconds: game.InitialTimeSeconds,
            IncrementSeconds: game.IncrementSeconds);

        return OutboxMessage.Create(
            game.Id,
            GameFinishedType,
            JsonSerializer.Serialize(finishedEvent, JsonOptions));
    }

    public static OutboxMessage CreateMatched(GameEntity game, string category)
    {
        // The outbox id must not be the game id: the finished/aborted event of the same game
        // already uses it as its primary key.
        var eventId = Guid.NewGuid();

        var matchedEvent = new GameMatchedEvent(
            EventId: eventId,
            GameId: game.Id,
            WhitePlayerId: game.WhitePlayerId,
            BlackPlayerId: game.BlackPlayerId,
            InitialSeconds: game.InitialTimeSeconds,
            IncrementSeconds: game.IncrementSeconds,
            Category: category,
            Rated: game.Rated,
            OccurredAtUtc: DateTime.UtcNow);

        return OutboxMessage.Create(
            eventId,
            GameMatchedType,
            JsonSerializer.Serialize(matchedEvent, JsonOptions));
    }

    public static OutboxMessage CreateAborted(GameEntity game, string reason)
    {
        if (game.Status != GameStatus.Aborted)
            throw new InvalidOperationException("Only an aborted game can produce an aborted event.");

        var abortedEvent = new GameAbortedEvent(
            EventId: game.Id,
            GameId: game.Id,
            WhitePlayerId: game.WhitePlayerId,
            BlackPlayerId: game.BlackPlayerId,
            Reason: reason,
            OccurredAtUtc: game.FinishedAt ?? DateTime.UtcNow);

        return OutboxMessage.Create(
            game.Id,
            GameAbortedType,
            JsonSerializer.Serialize(abortedEvent, JsonOptions));
    }
}
