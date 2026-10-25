using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Realtime.Hubs;

/// <summary>
/// Maps a SignalR connection to the platform user GUID, using the same claim lookup as the
/// hub and the Game API, so Clients.User(userId) reaches every open tab of that user.
/// </summary>
public sealed class ChessUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? connection.User?.FindFirst("sub")?.Value;
}
