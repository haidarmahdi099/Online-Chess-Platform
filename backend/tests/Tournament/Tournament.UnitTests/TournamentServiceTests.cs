using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tournament.Application.DTOs;
using Tournament.Application.Interfaces;
using Tournament.Application.Services;
using Tournament.Domain.Entities;
using Tournament.Domain.Enums;

namespace Tournament.UnitTests;

public sealed class TournamentServiceTests
{
    private readonly Mock<ITournamentRepository> _repository = new();
    private readonly Mock<IGameProvisioner> _provisioner = new();
    private readonly TournamentService _service;

    public TournamentServiceTests() => _service = new TournamentService(
        _repository.Object, _provisioner.Object, NullLogger<TournamentService>.Instance);

    [Fact]
    public async Task Create_trims_user_text_and_returns_created_tournament()
    {
        var owner = Guid.NewGuid();
        ChessTournament? captured = null;
        _repository.Setup(r => r.CreateAsync(It.IsAny<ChessTournament>(), It.IsAny<CancellationToken>()))
            .Callback<ChessTournament, CancellationToken>((t, _) => captured = t)
            .ReturnsAsync((ChessTournament t, CancellationToken _) => t);
        var request = new CreateTournamentRequest("  Saturday Swiss  ", "  open event  ",
            DateTime.UtcNow.AddHours(2), 300, 2, 32);

        var result = await _service.CreateAsync(request, owner, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Saturday Swiss", captured.Name);
        Assert.Equal("open event", captured.Description);
        Assert.Equal(owner, result.CreatedByUserId);
        Assert.Equal("Upcoming", result.Status);
        Assert.Equal(300, result.InitialTimeSeconds);
        Assert.Equal(2, result.IncrementSeconds);
        Assert.Equal(32, result.MaxParticipants);
    }

    [Theory]
    [InlineData("", 180, 0, 16)]
    [InlineData("valid", 9, 0, 16)]
    [InlineData("valid", 180, -1, 16)]
    [InlineData("valid", 180, 0, 1)]
    public async Task Create_rejects_invalid_name_or_time_control_or_capacity(string name, int initial, int increment, int capacity)
    {
        var request = new CreateTournamentRequest(name, null, DateTime.UtcNow.AddHours(1), initial, increment, capacity);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(request, Guid.NewGuid(), CancellationToken.None));
        _repository.Verify(r => r.CreateAsync(It.IsAny<ChessTournament>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_unspecified_or_past_start_time()
    {
        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Unspecified);
        var past = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateTournamentRequest("name", null, unspecified), Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateTournamentRequest("name", null, past), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Join_maps_repository_outcomes_into_client_result()
    {
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _repository.Setup(r => r.JoinAsync(tournamentId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JoinAttemptResult(JoinTournamentOutcome.AlreadyJoined, 3));

        var result = await _service.JoinAsync(tournamentId, userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(JoinTournamentOutcome.AlreadyJoined, result.Outcome);
        Assert.False(result.Joined);
        Assert.Equal(3, result.ParticipantCount);
        Assert.Contains("already joined", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_provisions_pending_matches_only_after_repository_starts_tournament()
    {
        var tournamentId = Guid.NewGuid();
        var match = new PendingProvision(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), 180, 0);
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.StartAsync(tournamentId, It.IsAny<DateTime>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StartTournamentOutcome.Started);
        _repository.Setup(r => r.GetPendingProvisionsAsync(tournamentId, It.IsAny<DateTime>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PendingProvision>)new[] { match });
        _provisioner.Setup(p => p.ProvisionAsync(match, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionResult(ProvisionOutcome.Created, gameId));

        var result = await _service.StartAsync(tournamentId, CancellationToken.None);

        Assert.Equal(StartTournamentOutcome.Started, result);
        _repository.Verify(r => r.AttachGameAsync(tournamentId, match.MatchId, gameId, It.IsAny<CancellationToken>()), Times.Once);
        _provisioner.Verify(p => p.ProvisionAsync(match, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_does_not_provision_when_tournament_is_not_ready_to_start()
    {
        var tournamentId = Guid.NewGuid();
        _repository.Setup(r => r.StartAsync(tournamentId, It.IsAny<DateTime>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StartTournamentOutcome.TooEarly);

        var result = await _service.StartAsync(tournamentId, CancellationToken.None);

        Assert.Equal(StartTournamentOutcome.TooEarly, result);
        _repository.Verify(r => r.GetPendingProvisionsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _provisioner.Verify(p => p.ProvisionAsync(It.IsAny<PendingProvision>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
