using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Analysis.Domain.Evaluation;

namespace Analysis.IntegrationTests;

[Collection("Analysis API")]
public sealed class AnalysisApiTests(AnalysisApiFixture fixture)
{
    private const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public async Task Evaluation_requires_jwt_and_passes_analysis_result_over_http()
    {
        fixture.Engine.Reset();
        using var anonymous = fixture.CreateClient();
        using var denied = await anonymous.PostAsJsonAsync("/analysis/evaluate", new { fen = InitialFen, depth = 8 });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(0, fixture.Engine.EvaluationCount);

        using var client = AuthenticatedClient();
        using var response = await client.PostAsJsonAsync("/analysis/evaluate", new { fen = InitialFen, depth = 8 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(InitialFen, json.RootElement.GetProperty("fen").GetString());
        Assert.Equal(10, json.RootElement.GetProperty("depth").GetInt32()); // Engine's completed depth.
        Assert.Equal("cp", json.RootElement.GetProperty("scoreType").GetString());
        Assert.Equal(24, json.RootElement.GetProperty("score").GetInt32());
        Assert.Equal("e2e4", json.RootElement.GetProperty("bestMove").GetString());
        Assert.Equal(InitialFen, fixture.Engine.LastPosition?.Fen);
        Assert.Equal(8, fixture.Engine.LastDepth);
    }

    [Fact]
    public async Task Invalid_fen_or_depth_returns_bad_request_without_calling_engine()
    {
        fixture.Engine.Reset();
        using var client = AuthenticatedClient();

        using var invalidFen = await client.PostAsJsonAsync("/analysis/evaluate", new { fen = "invalid", depth = 8 });
        using var lowDepth = await client.PostAsJsonAsync("/analysis/evaluate", new { fen = InitialFen, depth = 0 });
        using var highDepth = await client.PostAsJsonAsync("/analysis/evaluate", new { fen = InitialFen, depth = 21 });

        Assert.Equal(HttpStatusCode.BadRequest, invalidFen.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, lowDepth.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, highDepth.StatusCode);
        Assert.Equal(0, fixture.Engine.EvaluationCount);
    }

    [Fact]
    public async Task Black_to_move_score_is_white_relative_and_engine_unavailable_maps_to_503()
    {
        fixture.Engine.Reset();
        fixture.Engine.Evaluation = new PositionEvaluation(ScoreType.Mate, 3, 12, "e7e5", new[] { "e7e5" });
        using var client = AuthenticatedClient();
        const string blackToMove = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1";

        using var response = await client.PostAsJsonAsync("/analysis/evaluate", new { fen = blackToMove, depth = 12 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("mate", json.RootElement.GetProperty("scoreType").GetString());
        Assert.Equal(-3, json.RootElement.GetProperty("score").GetInt32());

        fixture.Engine.ThrowUnavailable = true;
        using var unavailable = await client.PostAsJsonAsync("/analysis/evaluate", new { fen = InitialFen, depth = 12 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
    }

    [Fact]
    public async Task Readiness_health_reflects_engine_availability()
    {
        fixture.Engine.Reset();
        using var client = fixture.CreateClient();

        using var healthy = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);

        fixture.Engine.Available = false;
        using var unhealthy = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unhealthy.StatusCode);
    }

    private HttpClient AuthenticatedClient()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        return client;
    }

    private static string CreateToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AnalysisApiFixture.JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "ChessPlatform.Identity.Tests",
            audience: "ChessPlatform.Identity.Tests",
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
