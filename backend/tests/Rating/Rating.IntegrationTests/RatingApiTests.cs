using Rating.Application.Events;
using System.Net;
using System.Text.Json;

namespace Rating.IntegrationTests;

[Collection("Rating API")]
public sealed class RatingApiTests(RatingApiFixture fixture)
{
    [Fact]
    public async Task Profile_returns_defaults_and_history_rejects_invalid_query()
    {
        await fixture.ResetDatabaseAsync();
        var playerId = Guid.NewGuid();
        using var client = fixture.CreateClient();

        using var profile = await client.GetAsync($"/ratings/{playerId}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var profileJson = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.Equal(playerId, profileJson.RootElement.GetProperty("playerId").GetGuid());
        Assert.Equal(1200, profileJson.RootElement.GetProperty("chess").GetProperty("rating").GetInt32());
        Assert.Equal(1200, profileJson.RootElement.GetProperty("puzzle").GetProperty("rating").GetInt32());

        using var invalidCategory = await client.GetAsync($"/ratings/{playerId}/history?category=unknown");
        using var invalidPage = await client.GetAsync($"/ratings/{playerId}/history?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCategory.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
    }

    [Fact]
    public async Task Processed_game_and_puzzle_events_appear_in_profile_history_and_leaderboards_idempotently()
    {
        await fixture.ResetDatabaseAsync();
        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        var gameEvent = new GameFinishedEvent(Guid.NewGuid(), Guid.NewGuid(), white, black,
            "WhiteWin", DateTime.UtcNow, InitialTimeSeconds: 300);
        await fixture.ProcessGameAsync(gameEvent);
        await fixture.ProcessGameAsync(gameEvent); // Duplicate delivery must not double-rate.

        var puzzleEvent = new PuzzleAttemptedEvent(Guid.NewGuid(), Guid.NewGuid(), white,
            Guid.NewGuid(), true, DateTime.UtcNow, PuzzleRating: 1700);
        await fixture.ProcessPuzzleAsync(puzzleEvent);

        using var client = fixture.CreateClient();
        using var whiteProfile = await client.GetAsync($"/ratings/{white}");
        using var whiteJson = JsonDocument.Parse(await whiteProfile.Content.ReadAsStringAsync());
        Assert.Equal(1216, whiteJson.RootElement.GetProperty("chess").GetProperty("rating").GetInt32());
        Assert.Equal(1, whiteJson.RootElement.GetProperty("chess").GetProperty("gamesPlayed").GetInt32());
        Assert.Equal(1216, whiteJson.RootElement.GetProperty("blitz").GetProperty("rating").GetInt32());
        Assert.True(whiteJson.RootElement.GetProperty("puzzle").GetProperty("rating").GetInt32() > 1200);

        using var history = await client.GetAsync($"/ratings/{white}/history?category=blitz&page=1&pageSize=10");
        using var historyJson = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(1, historyJson.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(16, historyJson.RootElement.GetProperty("items")[0].GetProperty("change").GetInt32());

        using var leaderboard = await client.GetAsync("/leaderboard?category=blitz&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, leaderboard.StatusCode);
        using var leaderboardJson = JsonDocument.Parse(await leaderboard.Content.ReadAsStringAsync());
        Assert.Equal(2, leaderboardJson.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(white, leaderboardJson.RootElement.GetProperty("items")[0].GetProperty("playerId").GetGuid());
        Assert.Equal(1, leaderboardJson.RootElement.GetProperty("items")[0].GetProperty("rank").GetInt32());

        using var invalidLeaderboard = await client.GetAsync("/leaderboard?category=unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLeaderboard.StatusCode);

        var counts = await fixture.GetCountsAsync();
        Assert.Equal(5, counts.Ratings); // Chess + Blitz for both players, plus puzzle for White.
        Assert.Equal(5, counts.History);
        Assert.Equal(2, counts.Events);
    }
}
