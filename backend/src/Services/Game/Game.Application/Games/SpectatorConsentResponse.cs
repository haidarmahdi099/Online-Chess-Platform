namespace Game.Application.Games;

public sealed record SpectatorConsentResponse(
    bool GameStarted,
    bool ConsentResolved,
    bool SpectatorsAllowed,
    bool ConsentRejected);