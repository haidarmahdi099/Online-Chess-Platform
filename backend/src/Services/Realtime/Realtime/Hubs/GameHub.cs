
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Realtime.Hubs;

[Authorize]
public sealed class GameHub : Hub
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GamePresenceTracker _presenceTracker;
    private readonly ILogger<GameHub> _logger;
    private readonly IConfiguration _configuration;
    private readonly GameEndBroadcaster _gameEnd;

    public GameHub(IHttpClientFactory httpClientFactory, GamePresenceTracker presenceTracker,
                   ILogger<GameHub> logger, IConfiguration configuration,
                   GameEndBroadcaster gameEnd)
    {
        _httpClientFactory = httpClientFactory;
        _presenceTracker = presenceTracker;
        _logger = logger;
        _configuration = configuration;
        _gameEnd = gameEnd;
    }

    [HubMethodName("auth:refresh")]
    public Task RefreshAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 8192)
            throw new HubException("A valid access token is required.");

        var secret = _configuration["Jwt:SecretKey"];
        var issuer = _configuration["Jwt:Issuer"] ?? "ChessPlatform.Identity";
        var audience = _configuration["Jwt:Audience"] ?? "ChessPlatform";
        if (string.IsNullOrWhiteSpace(secret))
            throw new HubException("Token refresh is unavailable.");

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(accessToken,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = true, ValidIssuer = issuer,
                    ValidateAudience = true, ValidAudience = audience,
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                }, out _);

            var refreshedUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(refreshedUserId) ||
                !string.Equals(refreshedUserId, GetAuthenticatedUserId(), StringComparison.Ordinal))
                throw new HubException("The refreshed token belongs to a different user.");

            Context.Items["RefreshedAccessToken"] = accessToken;
            return Task.CompletedTask;
        }
        catch (HubException) { throw; }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogInformation("Rejected invalid access-token refresh for connection {ConnectionId}.", Context.ConnectionId);
            throw new HubException("The refreshed token is invalid or expired.");
        }
    }

    [HubMethodName("game:join")]
    public async Task JoinGame(Guid gameId)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("Authentication token is missing.");

        var client = _httpClientFactory.CreateClient("GameApi");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, Context.ConnectionAborted);

        await EnsureSuccessAsync(response, "join");

        // Same contract as spectators get: the joining caller receives the current game state,
        // so a client (or a reconnect) never has to make a separate REST call to render the board.
        var groupName = GetGroupName(gameId);
        var userId = GetAuthenticatedUserId();

        var connectionId = Context.ConnectionId;
        var existing = _presenceTracker.GetConnection(connectionId);

        // A connection must not remain in a spectator group
        // when it joins a game as a player.
        if (Context.Items.TryGetValue(SpectatorGameItemKey, out var watchedValue) &&
            watchedValue is Guid watchedGameId)
        {
            await Groups.RemoveFromGroupAsync(connectionId, GetSpectatorGroupName(watchedGameId));

            Context.Items.Remove(SpectatorGameItemKey);
        }

        if (existing is not null && existing.Value.GroupName == groupName &&
            existing.Value.UserId == userId)
        {
            // Re-read after the connection is already subscribed. This closes the gap where
            // moves could be broadcast between the first authorization read and group join.
            var currentSnapshot = await ReadSnapshotAsync($"games/{gameId}", token);
            await Clients.Caller.SendAsync("game:snapshot", currentSnapshot, Context.ConnectionAborted);
            await BroadcastClockAsync(gameId);
            return;
        }

        // Leave the previous game first, if necessary.
        if (existing is not null)
        {
            await Groups.RemoveFromGroupAsync(connectionId, existing.Value.GroupName);

            var previous = _presenceTracker.Leave(connectionId);

            if (previous is not null)
            {
                var onlineUsers = await _presenceTracker.TrackLeaveAsync(
                    previous.Value.GroupName, connectionId, previous.Value.OnlineUsers);
                try
                {
                    await Clients.Group(previous.Value.GroupName)
                        .SendAsync("presence", new
                        {
                            GameId = ParseGameId(previous.Value.GroupName),
                            OnlineUsers = onlineUsers
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                                       "Failed to broadcast presence after connection " +
                                           "{ConnectionId} left its previous game.",
                                       connectionId);
                }
            }
        }

        var groupAddAttempted = false;
        var trackerJoined = false;

        try
        {
            groupAddAttempted = true;

            await Groups.AddToGroupAsync(connectionId, groupName);

            var joined = _presenceTracker.Join(groupName, userId, connectionId);

            trackerJoined = true;
            var onlineUsers = await _presenceTracker.TrackJoinAsync(
                groupName, userId, connectionId, joined.CurrentCount);

            // The first GET above authorizes the player. Fetch again after group membership so
            // updates during authorization/join are represented in the initial snapshot.
            var currentSnapshot = await ReadSnapshotAsync($"games/{gameId}", token);

            await Clients.Group(groupName).SendAsync(
                "presence", new { GameId = gameId, OnlineUsers = onlineUsers });

            await Clients.Caller.SendAsync("game:snapshot", currentSnapshot, Context.ConnectionAborted);

            await BroadcastClockAsync(gameId);
        }
        catch
        {
            // Compensate for a partially completed join.
            if (trackerJoined)
            {
                var left = _presenceTracker.Leave(connectionId);

                if (left is not null)
                {
                    var onlineUsers = left.Value.OnlineUsers;
                    try
                    {
                        onlineUsers = await _presenceTracker.TrackLeaveAsync(
                            left.Value.GroupName, connectionId, left.Value.OnlineUsers);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update shared presence during join cleanup for {ConnectionId}.", connectionId);
                    }
                    try
                    {
                        await Clients.Group(left.Value.GroupName)
                            .SendAsync("presence", new
                            {
                                GameId = ParseGameId(left.Value.GroupName),
                                OnlineUsers = onlineUsers
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                                           "Failed to broadcast presence during join cleanup " +
                                               "for connection {ConnectionId}.",
                                           connectionId);
                    }
                }
            }

            if (groupAddAttempted)
            {
                try
                {
                    await Groups.RemoveFromGroupAsync(connectionId, groupName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                                       "Failed to remove connection {ConnectionId} from " +
                                           "game group during join cleanup.",
                                       connectionId);
                }
            }

            throw;
        }
    }

    [HubMethodName("game:joinSpectator")]
    public async Task JoinSpectator(Guid gameId)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("Authentication token is missing.");

        var client = _httpClientFactory.CreateClient("GameApi");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/public");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, Context.ConnectionAborted);

        await EnsureSuccessAsync(response, "join spectator");

        var connectionId = Context.ConnectionId;

        // If this connection is currently a player, leave that game first.
        var existing = _presenceTracker.GetConnection(connectionId);

        if (existing is not null)
        {
            await Groups.RemoveFromGroupAsync(connectionId, existing.Value.GroupName);

            var previous = _presenceTracker.Leave(connectionId);

            if (previous is not null)
            {
                var onlineUsers = await _presenceTracker.TrackLeaveAsync(
                    previous.Value.GroupName, connectionId, previous.Value.OnlineUsers);
                try
                {
                    await Clients.Group(previous.Value.GroupName)
                        .SendAsync("presence", new
                        {
                            GameId = ParseGameId(previous.Value.GroupName),
                            OnlineUsers = onlineUsers
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                                       "Failed to broadcast presence after connection " +
                                           "{ConnectionId} left its player game.",
                                       connectionId);
                }
            }
        }

        // Remove any previous spectator membership for this connection.
        if (Context.Items.TryGetValue(SpectatorGameItemKey, out var previousValue) &&
            previousValue is Guid previousGameId)
        {
            await Groups.RemoveFromGroupAsync(connectionId, GetSpectatorGroupName(previousGameId));

            Context.Items.Remove(SpectatorGameItemKey);
        }

        var spectatorGroup = GetSpectatorGroupName(gameId);
        await Groups.AddToGroupAsync(connectionId, spectatorGroup);
        Context.Items[SpectatorGameItemKey] = gameId;

        try
        {
            // The first read confirms this is a public game. Fetch again after subscribing so
            // moves/results published during the group transition are reflected in the snapshot.
            var gameSnapshot = await ReadSnapshotAsync($"games/{gameId}/public", token);
            await Clients.Caller.SendAsync("game:snapshot", gameSnapshot, Context.ConnectionAborted);
        }
        catch
        {
            Context.Items.Remove(SpectatorGameItemKey);
            try
            {
                await Groups.RemoveFromGroupAsync(connectionId, spectatorGroup);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove spectator connection {ConnectionId} during join cleanup.", connectionId);
            }
            throw;
        }
    }

    [HubMethodName("game:leave")]
    public async Task LeaveGame(Guid gameId)
    {
        var connectionId = Context.ConnectionId;
        var groupName = GetGroupName(gameId);
        var spectatorGroupName = GetSpectatorGroupName(gameId);

        // Remove spectator membership if this connection is watching this game.
        if (Context.Items.TryGetValue(SpectatorGameItemKey, out var watchedGame) &&
            watchedGame is Guid watchedGameId && watchedGameId == gameId)
        {
            await Groups.RemoveFromGroupAsync(connectionId, spectatorGroupName);

            Context.Items.Remove(SpectatorGameItemKey);
        }

        // A spectator doesn't have a player presence-tracker entry.
        // Their leave is complete once removed from the spectator group.
        var connection = _presenceTracker.GetConnection(connectionId);

        if (connection is null || connection.Value.GroupName != groupName)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(connectionId, groupName);

        var left = _presenceTracker.Leave(connectionId);

        if (left is null)
            return;

        var onlineUsers = await _presenceTracker.TrackLeaveAsync(
            left.Value.GroupName, connectionId, left.Value.OnlineUsers);

        try
        {
            await Clients.Group(left.Value.GroupName)
                .SendAsync("presence",
                           new { GameId = gameId, OnlineUsers = onlineUsers });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                               "Failed to broadcast presence after connection " +
                                   "{ConnectionId} left game {GameId}.",
                               connectionId, gameId);
        }
    }

    private string? GetAccessToken()
    {
        if (Context.Items.TryGetValue("RefreshedAccessToken", out var refreshed) &&
            refreshed is string refreshedToken && !string.IsNullOrWhiteSpace(refreshedToken))
            return refreshedToken;

        var httpContext = Context.GetHttpContext();

        var authorization = httpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        var queryToken = httpContext?.Request.Query["access_token"].ToString();

        return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken;
    }

    private static string GetGroupName(Guid gameId) => $"game:{gameId}";

    private static string GetSpectatorGroupName(Guid gameId) => $"game-watch:{gameId}";

    private async Task<JsonElement> ReadSnapshotAsync(string path, string token)
    {
        var client = _httpClientFactory.CreateClient("GameApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, Context.ConnectionAborted);
        await EnsureSuccessAsync(response, "read game snapshot");
        var body = await response.Content.ReadAsStringAsync(Context.ConnectionAborted);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private const string SpectatorGameItemKey = "SpectatorGameId";

    private static Guid ParseGameId(string groupName)
    {
        if (groupName.StartsWith("game:", StringComparison.Ordinal) &&
            Guid.TryParse(groupName["game:".Length..], out var gameId))
        {
            return gameId;
        }

        return Guid.Empty;
    }

    [HubMethodName("game:respondSpectatorConsent")]
    public async Task RespondSpectatorConsent(Guid gameId, bool allowsSpectators)
    {
        EnsureJoinedGame(gameId);

        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("Authentication token is missing.");

        var client = _httpClientFactory.CreateClient("GameApi");

        using var request =
            new HttpRequestMessage(HttpMethod.Put, $"games/{gameId}/spectator-consent");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Content =
            System.Net.Http.Json.JsonContent.Create(new { AllowsSpectators = allowsSpectators });

        using var response = await client.SendAsync(request, Context.ConnectionAborted);

        await EnsureSuccessAsync(response, "spectator consent");

        var responseBody = await response.Content.ReadAsStringAsync(Context.ConnectionAborted);

        using var document = JsonDocument.Parse(responseBody);
        var result = document.RootElement.Clone();

        // Consent decisions are private to the two players.
        await Clients.Group(GetGroupName(gameId))
            .SendAsync("game:spectatorConsent", new { GameId = gameId, Result = result },
                       Context.ConnectionAborted);

        if (!IsTrue(result, "gameStarted"))
            return;

        // Notify both players that the game has started.
        await Clients.Group(GetGroupName(gameId))
            .SendAsync(
                "game:start",
                new
                {
                    GameId = gameId,
                    SpectatorsAllowed = IsTrue(result, "spectatorsAllowed"),
                    ConsentRejected = IsTrue(result, "consentRejected")
                },
                Context.ConnectionAborted);

        await BroadcastClockAsync(gameId);
    }

    [HubMethodName("game:move")]
    public async Task MakeMove(Guid gameId, string from, string to, string? promotion = null)
    {
        EnsureJoinedGame(gameId);

        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("Authentication token is missing.");

        var client = _httpClientFactory.CreateClient("GameApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/moves");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Content = System.Net.Http.Json.JsonContent.Create(
            new { From = from, To = to, Promotion = promotion });

        using var response = await client.SendAsync(request, Context.ConnectionAborted);

        // 409 can mean "your clock ran out" or "game already over": announce the real outcome
        // right away instead of waiting for the Kafka round trip, then surface the error.
        if ((int)response.StatusCode == 409)
        {
            await AnnounceOutcomeAsync(gameId);
        }

        await EnsureSuccessAsync(response, "move");

        var responseBody = await response.Content.ReadAsStringAsync(Context.ConnectionAborted);

        using var document = JsonDocument.Parse(responseBody);
        var result = document.RootElement.Clone();

        await Clients.Groups(new[] { GetGroupName(gameId), GetSpectatorGroupName(gameId) })
            .SendAsync("game:move",
                       new
                       {
                           GameId = gameId,
                           From = from,
                           To = to,
                           Promotion = promotion,
                           Result = result
                       },
                       Context.ConnectionAborted);

        // A move by the opponent declines a pending draw offer; tell the offerer.
        if (IsTrue(result, "drawOfferDeclined"))
        {
            await Clients.Group(GetGroupName(gameId))
                .SendAsync("game:drawDeclined",
                           new { GameId = gameId, DeclinedBy = GetAuthenticatedUserId(), Reason = "move" },
                           Context.ConnectionAborted);
        }

        // Checkmate, stalemate, draw rules or a too-short rated game: report what the server
        // actually persisted instead of guessing from the move flags.
        if (IsTrue(result, "gameOver"))
        {
            await AnnounceOutcomeAsync(gameId);
        }

        await BroadcastClockAsync(gameId);
    }

    [HubMethodName("game:resign")]
    public async Task Resign(Guid gameId)
    {
        EnsureJoinedGame(gameId);

        await ForwardGameCommandAsync(gameId, "resign");

        await AnnounceOutcomeAsync(gameId);
    }

    [HubMethodName("game:offerDraw")]
    public async Task OfferDraw(Guid gameId)
    {
        EnsureJoinedGame(gameId);

        var result = await ForwardGameCommandAsync(gameId, "draw");

        await Clients.Group(GetGroupName(gameId))
            .SendAsync(
                "game:drawOffered",
                new { GameId = gameId, OfferedBy = GetAuthenticatedUserId(), Result = result },
                Context.ConnectionAborted);
    }

    [HubMethodName("game:acceptDraw")]
    public async Task AcceptDraw(Guid gameId)
    {
        EnsureJoinedGame(gameId);

        await ForwardGameCommandAsync(gameId, "draw/accept");

        await AnnounceOutcomeAsync(gameId);
    }

    [HubMethodName("game:declineDraw")]
    public async Task DeclineDraw(Guid gameId)
    {
        EnsureJoinedGame(gameId);

        var result = await ForwardGameCommandAsync(gameId, "draw/decline");

        await Clients.Group(GetGroupName(gameId))
            .SendAsync(
                "game:drawDeclined",
                new { GameId = gameId, DeclinedBy = GetAuthenticatedUserId(), Reason = "declined", Result = result },
                Context.ConnectionAborted);
    }

    private async Task<JsonElement> ForwardGameCommandAsync(Guid gameId, string command)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("Authentication token is missing.");

        var client = _httpClientFactory.CreateClient("GameApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/{command}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, Context.ConnectionAborted);

        await EnsureSuccessAsync(response, command);

        var responseBody = await response.Content.ReadAsStringAsync(Context.ConnectionAborted);

        using var document = JsonDocument.Parse(responseBody);

        return document.RootElement.Clone();
    }

    [HubMethodName("chat:send")]
    public async Task SendChat(Guid gameId, string message)
    {
        EnsureJoinedGame(gameId);

        if (string.IsNullOrWhiteSpace(message))
            throw new HubException("Message cannot be empty.");

        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("Authentication token is missing.");

        var client = _httpClientFactory.CreateClient("GameApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"games/{gameId}/chat");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Content = System.Net.Http.Json.JsonContent.Create(new { Message = message });

        using var response = await client.SendAsync(request, Context.ConnectionAborted);

        await EnsureSuccessAsync(response, "chat");

        await Clients.Group(GetGroupName(gameId))
            .SendAsync("chat:message",
                       new
                       {
                           GameId = gameId,
                           UserId = GetAuthenticatedUserId(),
                           Message = message,
                           SentAtUtc = DateTimeOffset.UtcNow
                       },
                       Context.ConnectionAborted);
    }

    [HubMethodName("tournament:join")]
    public async Task JoinTournament(Guid tournamentId)
    {
        if (tournamentId == Guid.Empty) throw new HubException("A valid tournament id is required.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tournament:{tournamentId:D}", Context.ConnectionAborted);
    }

    [HubMethodName("tournament:leave")]
    public async Task LeaveTournament(Guid tournamentId)
    {
        if (tournamentId == Guid.Empty) throw new HubException("A valid tournament id is required.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tournament:{tournamentId:D}", Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var left = _presenceTracker.Leave(Context.ConnectionId);

            if (left is not null)
            {
                var onlineUsers = await _presenceTracker.TrackLeaveAsync(
                    left.Value.GroupName, Context.ConnectionId, left.Value.OnlineUsers);
                try
                {
                    await Clients.Group(left.Value.GroupName)
                        .SendAsync("presence", new
                        {
                            GameId = ParseGameId(left.Value.GroupName),
                            OnlineUsers = onlineUsers
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                                       "Failed to broadcast presence after connection " +
                                           "{ConnectionId} disconnected.",
                                       Context.ConnectionId);
                }
            }

            if (exception is not null)
            {
                _logger.LogDebug(exception, "SignalR connection {ConnectionId} disconnected.",
                                 Context.ConnectionId);
            }
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    private string GetAuthenticatedUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               throw new HubException("User identity is missing.");
    }

    private void EnsureJoinedGame(Guid gameId)
    {
        var expectedGroup = GetGroupName(gameId);

        var connection = _presenceTracker.GetConnection(Context.ConnectionId);

        if (connection is null || connection.Value.GroupName != expectedGroup ||
            connection.Value.UserId != GetAuthenticatedUserId())
        {
            throw new HubException("Join this game before sending commands.");
        }
    }

    private async Task BroadcastClockAsync(Guid gameId)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Cannot broadcast clock for game {GameId}: access token is missing.",
                               gameId);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("GameApi");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}/clock");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, Context.ConnectionAborted);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clock retrieval failed for game {GameId}. HTTP {StatusCode}.",
                                   gameId, (int)response.StatusCode);
                return;
            }

            var clock = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: Context.ConnectionAborted);

            // Send the clock to both players and spectators.
            await Clients.Groups(GetGroupName(gameId), GetSpectatorGroupName(gameId))
                .SendAsync("game:clock", clock, Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
            when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The client disconnected.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast clock for game {GameId}.", gameId);
        }
    }

    /// <summary>
    /// Reads the persisted game and announces its real outcome (finished or aborted) exactly once.
    /// The Kafka consumer uses the same broadcaster, so whichever path is first wins.
    /// </summary>
    private async Task AnnounceOutcomeAsync(Guid gameId)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Cannot announce the outcome of game {GameId}: access token is missing.", gameId);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("GameApi");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"games/{gameId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, Context.ConnectionAborted);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Outcome lookup failed for game {GameId}. HTTP {StatusCode}.",
                                   gameId, (int)response.StatusCode);
                return;
            }

            var game = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: Context.ConnectionAborted);

            var status = GetString(game, "status");

            if (string.Equals(status, "Finished", StringComparison.Ordinal))
            {
                var winnerId = GetGuid(game, "winnerId");
                var whiteId = GetGuid(game, "whitePlayerId");
                var result = winnerId is null ? "Draw" : winnerId == whiteId ? "WhiteWin" : "BlackWin";

                await _gameEnd.AnnounceFinishedAsync(
                    new GameEndInfo(gameId, winnerId, result, GetString(game, "endReason"), IsTrue(game, "rated")));
            }
            else if (string.Equals(status, "Aborted", StringComparison.Ordinal))
            {
                await _gameEnd.AnnounceAbortedAsync(gameId, "Aborted");
            }
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // The client disconnected; the Kafka consumer will still announce the outcome.
        }
        catch (Exception ex)
        {
            // The command already succeeded; the Kafka consumer is the fallback announcer.
            _logger.LogError(ex, "Failed to announce the outcome of game {GameId}.", gameId);
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static Guid? GetGuid(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool IsTrue(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.True;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        var statusCode = (int)response.StatusCode;
        var errorBody = await response.Content.ReadAsStringAsync(Context.ConnectionAborted);

        _logger.LogWarning("Game API operation {Operation} failed with HTTP {StatusCode}.",
                           operation, statusCode);

        var message = statusCode switch
        {
            400 => "The request was invalid.",
            401 => "Authentication with the game service failed.",
            403 => "You are not allowed to perform this action.",
            404 => "The game was not found or you cannot access it.",
            409 => "The game state changed or the action conflicts with it.",
            _ => "The game service could not complete the request."
        };

        throw new HubException(message);
    }
}
