using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Game.IntegrationTests;

public sealed class GameApiTests(GameApiFixture fixture) : IClassFixture<GameApiFixture>
{
    private const string JwtSecret = "integration-tests-only-signing-key-at-least-32-bytes";
    private const int InitialSeconds = 300;
    private const int IncrementSeconds = 2;

    [Fact]
    public async Task AuthorizationAndPublicRoutes_EnforceExpectedAccess()
    {
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/games")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/games/seek/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/internal/tournament-games", new { })).StatusCode);

        var playerId = Guid.NewGuid();
        using var publicStats = await anonymous.GetAsync($"/stats/{playerId}");
        Assert.Equal(HttpStatusCode.OK, publicStats.StatusCode);
        using var directCreateClient = CreatePlayerClient(playerId);
        using var directCreate = await directCreateClient.PostAsJsonAsync("/games", new
        {
            whitePlayerId = playerId,
            blackPlayerId = Guid.NewGuid(),
            initialTimeSeconds = InitialSeconds,
            incrementSeconds = IncrementSeconds
        });
        Assert.Equal(HttpStatusCode.Conflict, directCreate.StatusCode);
    }

    [Fact]
    public async Task TournamentGame_CanBeReadPlayedAndQueried_WithPlayerAuthorization()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var gameId = await CreateTournamentGameAsync(white, black);
        Assert.Equal(gameId, await CreateTournamentGameAsync(white, black, gameId));
        using (var idempotencyConflict = fixture.CreateClient())
        {
            idempotencyConflict.DefaultRequestHeaders.Add("X-Internal-Service-Key", fixture.InternalServiceKey);
            using var conflict = await idempotencyConflict.PostAsJsonAsync("/internal/tournament-games", new
            {
                gameId,
                whitePlayerId = white,
                blackPlayerId = Guid.NewGuid(),
                initialTimeSeconds = InitialSeconds,
                incrementSeconds = IncrementSeconds
            });
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        }
        using var whiteClient = CreatePlayerClient(white);

        using var game = await whiteClient.GetAsync($"/games/{gameId}");
        Assert.Equal(HttpStatusCode.OK, game.StatusCode);
        using (var gameJson = await JsonDocument.ParseAsync(await game.Content.ReadAsStreamAsync()))
            Assert.Equal("Live", gameJson.RootElement.GetProperty("status").GetString());

        using var clock = await whiteClient.GetAsync($"/games/{gameId}/clock");
        Assert.Equal(HttpStatusCode.OK, clock.StatusCode);
        using var moves = await whiteClient.GetAsync($"/games/{gameId}/moves");
        Assert.Equal(HttpStatusCode.OK, moves.StatusCode);
        using (var movesJson = await JsonDocument.ParseAsync(await moves.Content.ReadAsStreamAsync()))
            Assert.Empty(movesJson.RootElement.GetProperty("moves").EnumerateArray());
        using var pgn = await whiteClient.GetAsync($"/games/{gameId}/pgn");
        Assert.Equal(HttpStatusCode.OK, pgn.StatusCode);
        Assert.Contains("[Event", await pgn.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var gamesPage = await whiteClient.GetAsync("/games?filter=mine&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, gamesPage.StatusCode);
        using var badFilter = await whiteClient.GetAsync("/games?filter=all");
        Assert.Equal(HttpStatusCode.BadRequest, badFilter.StatusCode);
        using var badPage = await whiteClient.GetAsync("/games?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);

        using var emptyPublic = await whiteClient.GetAsync($"/games/{gameId}/public");
        Assert.Equal(HttpStatusCode.NotFound, emptyPublic.StatusCode);
        using var notPlayer = CreatePlayerClient(Guid.NewGuid());
        using var hidden = await notPlayer.GetAsync($"/games/{gameId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var sendChat = await whiteClient.PostAsJsonAsync($"/games/{gameId}/chat", new { message = "Good luck!" });
        Assert.Equal(HttpStatusCode.OK, sendChat.StatusCode);
        using var chat = await whiteClient.GetAsync($"/games/{gameId}/chat");
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
        using (var chatJson = await JsonDocument.ParseAsync(await chat.Content.ReadAsStreamAsync()))
            Assert.Single(chatJson.RootElement.EnumerateArray());

        using var wrongTurn = await CreatePlayerClient(black).PostAsJsonAsync($"/games/{gameId}/moves", new { from = "e7", to = "e5" });
        Assert.Equal(HttpStatusCode.Conflict, wrongTurn.StatusCode);
        using var whiteMove = await whiteClient.PostAsJsonAsync($"/games/{gameId}/moves", new { from = "e2", to = "e4" });
        Assert.Equal(HttpStatusCode.OK, whiteMove.StatusCode);
        using var blackMove = await CreatePlayerClient(black).PostAsJsonAsync($"/games/{gameId}/moves", new { from = "e7", to = "e5" });
        Assert.Equal(HttpStatusCode.OK, blackMove.StatusCode);
        using var updatedMoves = await whiteClient.GetAsync($"/games/{gameId}/moves");
        using var updatedMovesJson = await JsonDocument.ParseAsync(await updatedMoves.Content.ReadAsStreamAsync());
        Assert.Equal(2, updatedMovesJson.RootElement.GetProperty("moves").GetArrayLength());

        using var liveGames = await whiteClient.GetAsync("/games?filter=live&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, liveGames.StatusCode);
        using var stats = await whiteClient.GetAsync("/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
    }

    [Fact]
    public async Task DrawResignationAndAbort_ApplyTerminalGameStates()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var drawGame = await CreateTournamentGameAsync(white, black);
        using var whiteClient = CreatePlayerClient(white);
        using var blackClient = CreatePlayerClient(black);

        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PostAsync($"/games/{drawGame}/draw", null)).StatusCode);
        using var ownAccept = await whiteClient.PostAsync($"/games/{drawGame}/draw/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, ownAccept.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blackClient.PostAsync($"/games/{drawGame}/draw/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PostAsync($"/games/{drawGame}/draw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blackClient.PostAsync($"/games/{drawGame}/draw/accept", null)).StatusCode);
        using var drawn = await whiteClient.GetAsync($"/games/{drawGame}");
        using (var drawnJson = await JsonDocument.ParseAsync(await drawn.Content.ReadAsStreamAsync()))
            Assert.Equal("Finished", drawnJson.RootElement.GetProperty("status").GetString());

        var resignGame = await CreateTournamentGameAsync(white, black);
        using var resign = await whiteClient.PostAsync($"/games/{resignGame}/resign", null);
        Assert.Equal(HttpStatusCode.OK, resign.StatusCode);
        using var resigned = await whiteClient.GetAsync($"/games/{resignGame}");
        using (var resignedJson = await JsonDocument.ParseAsync(await resigned.Content.ReadAsStreamAsync()))
            Assert.Equal(black, resignedJson.RootElement.GetProperty("winnerId").GetGuid());

        var abortGame = await CreateTournamentGameAsync(white, black);
        using var abort = await blackClient.PostAsync($"/games/{abortGame}/abort", null);
        Assert.Equal(HttpStatusCode.OK, abort.StatusCode);
        using var aborted = await whiteClient.GetAsync($"/games/{abortGame}");
        using (var abortedJson = await JsonDocument.ParseAsync(await aborted.Content.ReadAsStreamAsync()))
            Assert.Equal("Aborted", abortedJson.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SpectatorVisibility_RequiresBothPlayersConsent()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var gameId = await CreateTournamentGameAsync(white, black);
        using var anonymous = fixture.CreateClient();
        using var whiteClient = CreatePlayerClient(white);
        using var blackClient = CreatePlayerClient(black);
        using var outsider = CreatePlayerClient(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/games/{gameId}/public")).StatusCode);
        using var whiteConsent = await whiteClient.PutAsJsonAsync($"/games/{gameId}/spectator-consent", new { allowsSpectators = true });
        Assert.Equal(HttpStatusCode.OK, whiteConsent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/games/{gameId}/public")).StatusCode);
        using var outsiderConsent = await outsider.PutAsJsonAsync($"/games/{gameId}/spectator-consent", new { allowsSpectators = true });
        Assert.Equal(HttpStatusCode.NotFound, outsiderConsent.StatusCode);
        using var blackConsent = await blackClient.PutAsJsonAsync($"/games/{gameId}/spectator-consent", new { allowsSpectators = true });
        Assert.Equal(HttpStatusCode.OK, blackConsent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/games/{gameId}/public")).StatusCode);
    }

    [Fact]
    public async Task Matchmaking_CreatesOneLiveGameAndSupportsStatusAndCancellation()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var firstClient = CreatePlayerClient(first);
        using var secondClient = CreatePlayerClient(second);
        var seekRequest = new { timeControl = new { initialSeconds = 300, incrementSeconds = 0, category = "blitz" }, rated = false };

        using var firstSeek = await firstClient.PostAsJsonAsync("/games/seek", seekRequest);
        Assert.Equal(HttpStatusCode.Accepted, firstSeek.StatusCode);
        using var firstSeekJson = await JsonDocument.ParseAsync(await firstSeek.Content.ReadAsStreamAsync());
        Assert.Equal("Searching", firstSeekJson.RootElement.GetProperty("status").GetString());
        using var status = await firstClient.GetAsync("/games/seek/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        using var duplicate = await firstClient.PostAsJsonAsync("/games/seek", seekRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var secondSeek = await secondClient.PostAsJsonAsync("/games/seek", seekRequest);
        Assert.Equal(HttpStatusCode.Accepted, secondSeek.StatusCode);
        using var secondSeekJson = await JsonDocument.ParseAsync(await secondSeek.Content.ReadAsStreamAsync());
        Assert.Equal("Matched", secondSeekJson.RootElement.GetProperty("status").GetString());
        var gameId = secondSeekJson.RootElement.GetProperty("gameId").GetGuid();
        using var game = await firstClient.GetAsync($"/games/{gameId}");
        Assert.Equal(HttpStatusCode.OK, game.StatusCode);

        var third = Guid.NewGuid();
        using var thirdClient = CreatePlayerClient(third);
        using var thirdSeek = await thirdClient.PostAsJsonAsync("/games/seek", seekRequest);
        Assert.Equal(HttpStatusCode.Accepted, thirdSeek.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await thirdClient.DeleteAsync("/games/seek")).StatusCode);
        using var cancelledStatus = await thirdClient.GetAsync("/games/seek/status");
        Assert.Equal(HttpStatusCode.OK, cancelledStatus.StatusCode);
        using var missingCancel = await thirdClient.DeleteAsync("/games/seek");
        Assert.Equal(HttpStatusCode.NotFound, missingCancel.StatusCode);

        using var invalidSeek = await CreatePlayerClient(Guid.NewGuid()).PostAsJsonAsync("/games/seek", new { timeControl = new { initialSeconds = 5, incrementSeconds = 0, category = "blitz" }, rated = false });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSeek.StatusCode);
    }

    [Fact]
    public async Task Challenges_CanBeCreatedAcceptedDeclinedAndCancelled()
    {
        var challenger = Guid.NewGuid();
        var opponent = Guid.NewGuid();
        using var challengerClient = CreatePlayerClient(challenger);
        using var opponentClient = CreatePlayerClient(opponent);
        var request = new { opponentUserId = opponent, initialTimeSeconds = InitialSeconds, incrementSeconds = IncrementSeconds, rated = true };

        using var created = await challengerClient.PostAsJsonAsync("/challenges", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var challengeId = createdJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await challengerClient.PostAsJsonAsync("/challenges", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await opponentClient.GetAsync("/challenges/incoming")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await challengerClient.GetAsync("/challenges/outgoing")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await opponentClient.GetAsync($"/challenges/{challengeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreatePlayerClient(Guid.NewGuid()).PostAsync($"/challenges/{challengeId}/accept", null)).StatusCode);

        using var accepted = await opponentClient.PostAsync($"/challenges/{challengeId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedJson = await JsonDocument.ParseAsync(await accepted.Content.ReadAsStreamAsync());
        var gameId = acceptedJson.RootElement.GetProperty("gameId").GetGuid();
        using var acceptedGame = await challengerClient.GetAsync($"/games/{gameId}");
        using var acceptedGameJson = await JsonDocument.ParseAsync(await acceptedGame.Content.ReadAsStreamAsync());
        Assert.Equal("Waiting", acceptedGameJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await challengerClient.PutAsJsonAsync($"/games/{gameId}/spectator-consent", new { allowsSpectators = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await opponentClient.PutAsJsonAsync($"/games/{gameId}/spectator-consent", new { allowsSpectators = true })).StatusCode);
        using var startedGame = await challengerClient.GetAsync($"/games/{gameId}");
        using var startedGameJson = await JsonDocument.ParseAsync(await startedGame.Content.ReadAsStreamAsync());
        Assert.Equal("Live", startedGameJson.RootElement.GetProperty("status").GetString());

        using var declinedChallenge = await challengerClient.PostAsJsonAsync("/challenges", request with { opponentUserId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, declinedChallenge.StatusCode);
        using var declineJson = await JsonDocument.ParseAsync(await declinedChallenge.Content.ReadAsStreamAsync());
        var declineId = declineJson.RootElement.GetProperty("id").GetGuid();
        var secondOpponent = declineJson.RootElement.GetProperty("opponentUserId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await CreatePlayerClient(secondOpponent).PostAsync($"/challenges/{declineId}/decline", null)).StatusCode);

        using var cancelledChallenge = await challengerClient.PostAsJsonAsync("/challenges", request with { opponentUserId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, cancelledChallenge.StatusCode);
        using var cancelJson = await JsonDocument.ParseAsync(await cancelledChallenge.Content.ReadAsStreamAsync());
        var cancelId = cancelJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await challengerClient.DeleteAsync($"/challenges/{cancelId}")).StatusCode);
    }

    [Fact]
    public async Task Rematches_CanBeRequestedAcceptedDeclinedAndCancelled()
    {
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        using var whiteClient = CreatePlayerClient(white);
        using var blackClient = CreatePlayerClient(black);

        var original = await CreateTournamentGameAsync(white, black);
        Assert.Equal(HttpStatusCode.Conflict, (await whiteClient.PostAsync($"/games/{original}/rematch", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PostAsync($"/games/{original}/resign", null)).StatusCode);
        using var request = await whiteClient.PostAsync($"/games/{original}/rematch", null);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        using var requestJson = await JsonDocument.ParseAsync(await request.Content.ReadAsStreamAsync());
        var rematchId = requestJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await whiteClient.PostAsync($"/games/{original}/rematch", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blackClient.GetAsync("/games/rematches/incoming")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.GetAsync("/games/rematches/outgoing")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await whiteClient.PostAsync($"/games/rematches/{rematchId}/accept", null)).StatusCode);

        using var accepted = await blackClient.PostAsync($"/games/rematches/{rematchId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedJson = await JsonDocument.ParseAsync(await accepted.Content.ReadAsStreamAsync());
        var newGameId = acceptedJson.RootElement.GetProperty("rematchGameId").GetGuid();
        using var rematchGame = await whiteClient.GetAsync($"/games/{newGameId}");
        using var rematchGameJson = await JsonDocument.ParseAsync(await rematchGame.Content.ReadAsStreamAsync());
        Assert.Equal("Waiting", rematchGameJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PutAsJsonAsync($"/games/{newGameId}/spectator-consent", new { allowsSpectators = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blackClient.PutAsJsonAsync($"/games/{newGameId}/spectator-consent", new { allowsSpectators = false })).StatusCode);
        using var startedRematch = await whiteClient.GetAsync($"/games/{newGameId}");
        using var startedRematchJson = await JsonDocument.ParseAsync(await startedRematch.Content.ReadAsStreamAsync());
        Assert.Equal("Live", startedRematchJson.RootElement.GetProperty("status").GetString());

        var declinedOriginal = await CreateTournamentGameAsync(white, black);
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PostAsync($"/games/{declinedOriginal}/resign", null)).StatusCode);
        using var declineRequest = await whiteClient.PostAsync($"/games/{declinedOriginal}/rematch", null);
        using var declineRequestJson = await JsonDocument.ParseAsync(await declineRequest.Content.ReadAsStreamAsync());
        var declineId = declineRequestJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await blackClient.PostAsync($"/games/rematches/{declineId}/decline", null)).StatusCode);

        var cancelledOriginal = await CreateTournamentGameAsync(white, black);
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PostAsync($"/games/{cancelledOriginal}/resign", null)).StatusCode);
        using var cancelRequest = await whiteClient.PostAsync($"/games/{cancelledOriginal}/rematch", null);
        using var cancelRequestJson = await JsonDocument.ParseAsync(await cancelRequest.Content.ReadAsStreamAsync());
        var cancelId = cancelRequestJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await whiteClient.DeleteAsync($"/games/rematches/{cancelId}")).StatusCode);
    }

    private async Task<Guid> CreateTournamentGameAsync(Guid white, Guid black, Guid? gameId = null)
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Service-Key", fixture.InternalServiceKey);
        var id = gameId ?? Guid.NewGuid();
        using var response = await client.PostAsJsonAsync("/internal/tournament-games", new
        {
            gameId = id,
            whitePlayerId = white,
            blackPlayerId = black,
            initialTimeSeconds = InitialSeconds,
            incrementSeconds = IncrementSeconds
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(id, json.RootElement.GetProperty("gameId").GetGuid());
        return id;
    }

    private HttpClient CreatePlayerClient(Guid playerId)
    {
        var client = fixture.CreateClient();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            issuer: "ChessPlatform.Identity.Tests",
            audience: "ChessPlatform.Identity.Tests",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, playerId.ToString()),
                new Claim("sub", playerId.ToString())
            ],
            notBefore: DateTime.UtcNow.AddSeconds(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
