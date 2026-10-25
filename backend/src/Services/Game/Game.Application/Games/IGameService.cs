using Game.Application.Contracts;

namespace Game.Application.Games;

public interface IGameService
{
    Task<Guid> CreateGameAsync(
        CreateGameRequest request,
        CancellationToken cancellationToken);

    Task<Guid> CreateTournamentGameAsync(Guid gameId, CreateGameRequest request, CancellationToken cancellationToken);

    Task<MoveResult> MakeMoveAsync(
        Guid gameId,
        Guid playerId,
        MoveRequest request,
        CancellationToken cancellationToken);

    Task<GameActionResult> ResignAsync(
    Guid gameId,
    Guid playerId,
    CancellationToken cancellationToken);

    /// <summary>Cancels a live game before both players have moved. No result, no rating change.</summary>
    Task<GameActionResult> AbortAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken);

    Task<GameActionResult> OfferDrawAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken);

    Task<GameActionResult> AcceptDrawAsync(
    Guid gameId,
    Guid playerId,
    CancellationToken cancellationToken);

    Task<GameActionResult> DeclineDrawAsync(
        Guid gameId,
        Guid playerId,
        CancellationToken cancellationToken);

    Task<GameDetailsResponse?> GetGameAsync(
    Guid gameId,
    Guid playerId,
    CancellationToken cancellationToken);

    Task<IReadOnlyList<GameDetailsResponse>> GetGamesAsync(
        Guid? playerId,
        bool liveOnly,
        CancellationToken cancellationToken);

    Task<PagedResponse<GameDetailsResponse>> GetGamesPageAsync(
    Guid? playerId,
    bool liveOnly,
    int page,
    int pageSize,
    CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessageResponse>> GetChatAsync(
    Guid gameId,
    CancellationToken cancellationToken);

    Task<GameActionResult> SendChatMessageAsync(
    Guid gameId,
    Guid userId,
    ChatMessageRequest request,
    CancellationToken cancellationToken);

    Task<GameStatsResponse> GetStatsAsync(
    Guid playerId,
    CancellationToken cancellationToken);

    Task<GameClockResponse?> GetClockAsync(
    Guid gameId,
    Guid playerId,
    CancellationToken cancellationToken);


    /// <summary>Move list with SAN. Null when the game does not exist or the viewer may not see it.</summary>
    Task<GameMovesResponse?> GetMovesAsync(
        Guid gameId,
        Guid viewerId,
        CancellationToken cancellationToken);

    /// <summary>PGN text. Null when the game does not exist or the viewer may not see it.</summary>
    Task<string?> GetPgnAsync(
        Guid gameId,
        Guid viewerId,
        CancellationToken cancellationToken);

    Task<GameDetailsResponse?> GetPublicGameAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    Task<SpectatorConsentResponse?> RespondToSpectatorConsentAsync(
     Guid gameId,
     Guid playerId,
     bool allowsSpectators,
     CancellationToken cancellationToken);
}