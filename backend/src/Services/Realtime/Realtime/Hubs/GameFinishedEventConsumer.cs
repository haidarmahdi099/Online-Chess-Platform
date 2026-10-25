using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Realtime.Hubs;
using System.Text.Json;

namespace Realtime.Messaging;

public sealed class GameFinishedEventConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly GameEndBroadcaster _broadcaster;
    private readonly GameMatchBroadcaster _matchBroadcaster;
    private readonly ILogger<GameFinishedEventConsumer> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public GameFinishedEventConsumer(IConfiguration configuration, GameEndBroadcaster broadcaster,
        GameMatchBroadcaster matchBroadcaster, ILogger<GameFinishedEventConsumer> logger, IHubContext<GameHub> hubContext)
    {
        _configuration = configuration;
        _hubContext = hubContext;
        _broadcaster = broadcaster;
        _matchBroadcaster = matchBroadcaster;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(async () =>
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = _configuration["Kafka:ConsumerGroupId"] ?? "realtime-game-events",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };
        var finishedTopic = _configuration["Kafka:GameFinishedTopic"] ?? "game.finished";
        var abortedTopic = _configuration["Kafka:GameAbortedTopic"] ?? "game.aborted";
        var matchedTopic = _configuration["Kafka:GameMatchedTopic"] ?? "game.matched";
        var tournamentTopic = _configuration["Kafka:TournamentEventsTopic"] ?? "tournament.events";
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(new[] { finishedTopic, abortedTopic, matchedTopic, tournamentTopic });
        _logger.LogInformation(
            "Realtime game-event consumer started on {Finished}, {Aborted}, {Matched} and {Tournament}.",
            finishedTopic, abortedTopic, matchedTopic, tournamentTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (result is null || result.IsPartitionEOF)
                    continue;

                if (string.Equals(result.Topic, abortedTopic, StringComparison.Ordinal))
                    await HandleAbortedAsync(result.Message.Value, stoppingToken);
                else if (string.Equals(result.Topic, matchedTopic, StringComparison.Ordinal))
                    await HandleMatchedAsync(result.Message.Value, stoppingToken);
                else if (string.Equals(result.Topic, tournamentTopic, StringComparison.Ordinal))
                    await HandleTournamentEventAsync(result.Message.Value, stoppingToken);
                else
                    await HandleFinishedAsync(result.Message.Value, stoppingToken);

                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid game outcome event; skipping it.");
                if (result is not null) consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume failure in Realtime.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not broadcast game outcome; offset will not be committed.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }, stoppingToken);

    private async Task HandleTournamentEventAsync(string json, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("tournamentId", out var idElement) || !Guid.TryParse(idElement.GetString(), out var tournamentId) || tournamentId == Guid.Empty)
            throw new JsonException("Tournament event is missing a valid tournamentId.");
        var eventType = root.TryGetProperty("eventType", out var typeElement) ? typeElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(eventType)) throw new JsonException("Tournament event is missing eventType.");
        await _hubContext.Clients.Group($"tournament:{tournamentId:D}").SendAsync("tournament:event", root.Clone(), cancellationToken);
    }

    private async Task HandleFinishedAsync(string json, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<GameFinishedMessage>(json, _jsonOptions);
        if (message is null || message.GameId == Guid.Empty)
            throw new JsonException("Game-finished event is missing its game id.");

        await _broadcaster.AnnounceFinishedAsync(
            new GameEndInfo(message.GameId, message.WinnerId, message.Result, message.EndReason, message.Rated),
            cancellationToken);
    }

    private async Task HandleMatchedAsync(string json, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<GameMatchedMessage>(json, _jsonOptions);
        if (message is null || message.GameId == Guid.Empty)
            throw new JsonException("Game-matched event is missing its game id.");

        await _matchBroadcaster.AnnounceMatchedAsync(
            new GameMatchInfo(message.GameId, message.WhitePlayerId, message.BlackPlayerId,
                message.InitialSeconds, message.IncrementSeconds, message.Category ?? string.Empty,
                message.Rated),
            cancellationToken);
    }

    private async Task HandleAbortedAsync(string json, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<GameAbortedMessage>(json, _jsonOptions);
        if (message is null || message.GameId == Guid.Empty)
            throw new JsonException("Game-aborted event is missing its game id.");

        await _broadcaster.AnnounceAbortedAsync(message.GameId, message.Reason ?? "Aborted", cancellationToken);
    }

    private sealed record GameFinishedMessage(Guid EventId, Guid GameId, Guid WhitePlayerId,
        Guid BlackPlayerId, string Result, DateTime OccurredAtUtc, bool Rated = true,
        string? EndReason = null, Guid? WinnerId = null);

    private sealed record GameMatchedMessage(Guid EventId, Guid GameId, Guid WhitePlayerId,
        Guid BlackPlayerId, int InitialSeconds, int IncrementSeconds, string? Category,
        bool Rated, DateTime OccurredAtUtc);

    private sealed record GameAbortedMessage(Guid EventId, Guid GameId, Guid WhitePlayerId,
        Guid BlackPlayerId, string? Reason, DateTime OccurredAtUtc);
}
