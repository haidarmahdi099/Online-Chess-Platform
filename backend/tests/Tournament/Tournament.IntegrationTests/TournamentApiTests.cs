using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Tournament.IntegrationTests;

[Collection("Tournament API")]
public sealed class TournamentApiTests(TournamentApiFixture fixture)
{
    [Fact]
    public async Task Public_list_and_tournament_registration_enforce_access_and_duplicate_rules()
    {
        await fixture.ResetDatabaseAsync();
        var adminId = Guid.NewGuid();
        using var ordinaryUser = AuthenticatedClient(Guid.NewGuid());
        using var deniedCreate = await ordinaryUser.PostAsJsonAsync("/tournaments", CreateRequest());
        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);

        using var admin = AuthenticatedClient(adminId, isAdmin: true);
        using var created = await admin.PostAsJsonAsync("/tournaments", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var tournamentId = createdJson.RootElement.GetProperty("id").GetGuid();

        using var list = await fixture.CreateClient().GetAsync("/tournaments?page=1&pageSize=10&status=Upcoming");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, listJson.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal("Weekly Swiss", listJson.RootElement.GetProperty("items")[0].GetProperty("name").GetString());

        var firstPlayerId = Guid.NewGuid();
        using var firstPlayer = AuthenticatedClient(firstPlayerId);
        using var anonymous = fixture.CreateClient();
        using var anonymousJoin = await anonymous.PostAsync($"/tournaments/{tournamentId}/join", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousJoin.StatusCode);

        using var joined = await firstPlayer.PostAsync($"/tournaments/{tournamentId}/join", null);
        using var duplicate = await firstPlayer.PostAsync($"/tournaments/{tournamentId}/join", null);
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var myStatus = await firstPlayer.GetAsync($"/tournaments/{tournamentId}/me");
        using var statusJson = JsonDocument.Parse(await myStatus.Content.ReadAsStringAsync());
        Assert.True(statusJson.RootElement.GetProperty("isRegistered").GetBoolean());
        Assert.False(statusJson.RootElement.GetProperty("isOwner").GetBoolean());

        using var invalidPage = await anonymous.GetAsync("/tournaments?page=0");
        using var missing = await anonymous.GetAsync($"/tournaments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Start_pair_provision_result_and_standings_complete_full_lifecycle()
    {
        await fixture.ResetDatabaseAsync();
        var ownerId = Guid.NewGuid();
        using var owner = AuthenticatedClient(ownerId, isAdmin: true);
        using var create = await owner.PostAsJsonAsync("/tournaments", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var tournamentId = createdJson.RootElement.GetProperty("id").GetGuid();

        var white = Guid.NewGuid();
        var black = Guid.NewGuid();
        using var whiteClient = AuthenticatedClient(white);
        using var blackClient = AuthenticatedClient(black);
        Assert.Equal(HttpStatusCode.OK, (await whiteClient.PostAsync($"/tournaments/{tournamentId}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blackClient.PostAsync($"/tournaments/{tournamentId}/join", null)).StatusCode);

        await fixture.SetStartTimeInPastAsync(tournamentId);
        using var start = await owner.PostAsync($"/tournaments/{tournamentId}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var startedJson = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        Assert.Equal("InProgress", startedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, startedJson.RootElement.GetProperty("currentRound").GetInt32());
        var match = startedJson.RootElement.GetProperty("matches")[0];
        var matchId = match.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, match.GetProperty("gameId").GetGuid());
        Assert.Equal("InProgress", match.GetProperty("status").GetString());
        Assert.Equal(1, fixture.GameProvisioner.Calls);

        using var setResult = await owner.PutAsJsonAsync($"/tournaments/{tournamentId}/matches/{matchId}/result", new { result = "WhiteWin" });
        Assert.Equal(HttpStatusCode.OK, setResult.StatusCode);
        using var completedJson = JsonDocument.Parse(await setResult.Content.ReadAsStringAsync());
        Assert.Equal("Completed", completedJson.RootElement.GetProperty("status").GetString());

        using var standings = await fixture.CreateClient().GetAsync($"/tournaments/{tournamentId}/standings");
        Assert.Equal(HttpStatusCode.OK, standings.StatusCode);
        using var standingsJson = JsonDocument.Parse(await standings.Content.ReadAsStringAsync());
        var winnerStanding = standingsJson.RootElement.GetProperty("standings").EnumerateArray()
            .Single(row => row.GetProperty("userId").GetGuid() == match.GetProperty("whitePlayerId").GetGuid());
        Assert.Equal(1m, winnerStanding.GetProperty("points").GetDecimal());
        Assert.Equal(1, winnerStanding.GetProperty("wins").GetInt32());

        using var results = await fixture.CreateClient().GetAsync($"/tournaments/{tournamentId}/results");
        using var resultsJson = JsonDocument.Parse(await results.Content.ReadAsStringAsync());
        Assert.Single(resultsJson.RootElement.GetProperty("results").EnumerateArray());
    }

    private static object CreateRequest() => new
    {
        name = "  Weekly Swiss  ",
        description = "Friendly event",
        startsAtUtc = DateTime.UtcNow.AddDays(1),
        initialTimeSeconds = 300,
        incrementSeconds = 2,
        maxParticipants = 16
    };

    private HttpClient AuthenticatedClient(Guid userId, bool isAdmin = false)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId, isAdmin));
        return client;
    }

    private static string CreateToken(Guid userId, bool isAdmin)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TournamentApiFixture.JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        var token = new JwtSecurityToken(
            issuer: "ChessPlatform.Identity.Tests",
            audience: "ChessPlatform.Identity.Tests",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
