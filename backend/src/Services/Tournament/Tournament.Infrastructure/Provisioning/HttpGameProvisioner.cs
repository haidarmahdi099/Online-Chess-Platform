using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tournament.Application.Interfaces;

namespace Tournament.Infrastructure.Provisioning;

/// <summary>
/// Creates tournament games through the Game API's private endpoint. The match id is sent
/// as the game id, which makes every call idempotent: a retry after a lost response, a crash
/// or a double call returns the same game instead of creating a second one.
/// </summary>
public sealed class HttpGameProvisioner(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HttpGameProvisioner> logger) : IGameProvisioner
{
    public const string HttpClientName = "GameService";

    public async Task<ProvisionResult> ProvisionAsync(PendingProvision match, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["GameService:BaseUrl"];
        var serviceKey = configuration["InternalServices:TournamentKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey) ||
            !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root))
        {
            return new ProvisionResult(ProvisionOutcome.NotConfigured);
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, "internal/tournament-games"));
            request.Headers.Add("X-Internal-Service-Key", serviceKey);
            request.Content = JsonContent.Create(new
            {
                gameId = match.MatchId,
                whitePlayerId = match.WhitePlayerId,
                blackPlayerId = match.BlackPlayerId,
                initialTimeSeconds = match.InitialTimeSeconds,
                incrementSeconds = match.IncrementSeconds
            });

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<ProvisionedGame>(cancellationToken: cancellationToken);
                return payload is { GameId: var gameId } && gameId != Guid.Empty
                    ? new ProvisionResult(ProvisionOutcome.Created, gameId)
                    : new ProvisionResult(ProvisionOutcome.Transient, Error: "Game API returned an invalid response.");
            }

            var status = (int)response.StatusCode;
            if (status is 400 or 409 or 422)
            {
                var error = $"Game API rejected the game ({status}).";
                logger.LogError("Match {MatchId}: {Error}", match.MatchId, error);
                return new ProvisionResult(ProvisionOutcome.Permanent, Error: error);
            }

            if (status is 401 or 403)
                logger.LogError("Game API refused the internal service key (HTTP {Status}). Check InternalServices:TournamentKey on both services.", status);

            return new ProvisionResult(ProvisionOutcome.Transient, Error: $"Game API returned HTTP {status}.");
        }
        catch (HttpRequestException ex)
        {
            return new ProvisionResult(ProvisionOutcome.Transient, Error: $"Game API unreachable: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProvisionResult(ProvisionOutcome.Transient, Error: "Game API request timed out.");
        }
        catch (JsonException ex)
        {
            return new ProvisionResult(ProvisionOutcome.Transient, Error: $"Game API response was not valid JSON: {ex.Message}");
        }
    }

    private sealed record ProvisionedGame(Guid GameId);
}
