using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Puzzle.IntegrationTests;

[Collection("Puzzle API")]
public sealed class PuzzleApiTests(PuzzleApiFixture fixture)
{
    [Fact]
    public async Task Public_puzzle_endpoints_filter_and_do_not_expose_solution()
    {
        await fixture.ResetDatabaseAsync();
        var activePuzzleId = await fixture.SeedPuzzleAsync(rating: 1600, themes: "fork,check");
        await fixture.SeedPuzzleAsync(rating: 2200, isActive: false);
        using var client = fixture.CreateClient();

        using var next = await client.GetAsync("/puzzles/next");
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        using var nextJson = JsonDocument.Parse(await next.Content.ReadAsStringAsync());
        Assert.Equal(activePuzzleId, nextJson.RootElement.GetProperty("id").GetGuid());
        Assert.False(nextJson.RootElement.TryGetProperty("solutionMoves", out _));

        using var page = await client.GetAsync("/puzzles?page=1&pageSize=10&minRating=1500&maxRating=1700");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var pageJson = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        Assert.Equal(1, pageJson.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(activePuzzleId, pageJson.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());

        using var badPage = await client.GetAsync("/puzzles?page=0");
        using var badRatingRange = await client.GetAsync("/puzzles?minRating=1800&maxRating=1000");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badRatingRange.StatusCode);
    }

    [Fact]
    public async Task Attempt_requires_auth_and_only_awards_first_attempt()
    {
        await fixture.ResetDatabaseAsync();
        var puzzleId = await fixture.SeedPuzzleAsync();
        var userId = Guid.NewGuid();
        using var anonymous = fixture.CreateClient();
        using var unauthorized = await anonymous.PostAsJsonAsync($"/puzzles/{puzzleId}/attempt", new { moves = new[] { "e2e4" } });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var client = AuthenticatedClient(userId);
        using var first = await client.PostAsJsonAsync($"/puzzles/{puzzleId}/attempt", new { moves = new[] { "e2e4", "e7e5", "g1f3" } });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.True(firstJson.RootElement.GetProperty("isCorrect").GetBoolean());
        Assert.True(firstJson.RootElement.GetProperty("ratingAffected").GetBoolean());

        using var repeat = await client.PostAsJsonAsync($"/puzzles/{puzzleId}/attempt", new { moves = new[] { "d2d4" } });
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        using var repeatJson = JsonDocument.Parse(await repeat.Content.ReadAsStringAsync());
        Assert.False(repeatJson.RootElement.GetProperty("isCorrect").GetBoolean());
        Assert.False(repeatJson.RootElement.GetProperty("ratingAffected").GetBoolean());

        var counts = await fixture.GetCountsAsync(userId, puzzleId);
        Assert.Equal(2, counts.Attempts);
        Assert.Equal(1, counts.RatingAwards);
        Assert.Equal(1, counts.OutboxMessages);
    }

    [Fact]
    public async Task Step_flow_and_user_stats_history_reflect_persisted_attempts()
    {
        await fixture.ResetDatabaseAsync();
        var puzzleId = await fixture.SeedPuzzleAsync();
        var userId = Guid.NewGuid();
        using var client = AuthenticatedClient(userId);

        using var partial = await client.PostAsJsonAsync($"/puzzles/{puzzleId}/step", new { moves = new[] { "e2e4" } });
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        using var partialJson = JsonDocument.Parse(await partial.Content.ReadAsStringAsync());
        Assert.Equal("continue", partialJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("e7e5", partialJson.RootElement.GetProperty("opponentReply").GetString());
        Assert.False(partialJson.RootElement.GetProperty("ratingAffected").GetBoolean());

        using var solved = await client.PostAsJsonAsync($"/puzzles/{puzzleId}/step", new { moves = new[] { "e2e4", "g1f3" } });
        Assert.Equal(HttpStatusCode.OK, solved.StatusCode);
        using var solvedJson = JsonDocument.Parse(await solved.Content.ReadAsStringAsync());
        Assert.Equal("solved", solvedJson.RootElement.GetProperty("status").GetString());

        using var stats = await client.GetAsync("/puzzles/me/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        using var statsJson = JsonDocument.Parse(await stats.Content.ReadAsStringAsync());
        Assert.Equal(1, statsJson.RootElement.GetProperty("totalAttempts").GetInt32());
        Assert.Equal(1, statsJson.RootElement.GetProperty("correctAttempts").GetInt32());
        Assert.Equal(1, statsJson.RootElement.GetProperty("distinctPuzzlesSolved").GetInt32());

        using var history = await client.GetAsync("/puzzles/me/history?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var historyJson = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(1, historyJson.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(puzzleId, historyJson.RootElement.GetProperty("items")[0].GetProperty("puzzleId").GetGuid());

        using var badHistoryPage = await client.GetAsync("/puzzles/me/history?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, badHistoryPage.StatusCode);
    }

    private HttpClient AuthenticatedClient(Guid userId)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    private static string CreateToken(Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PuzzleApiFixture.JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "ChessPlatform.Identity.Tests",
            audience: "ChessPlatform.Identity.Tests",
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
