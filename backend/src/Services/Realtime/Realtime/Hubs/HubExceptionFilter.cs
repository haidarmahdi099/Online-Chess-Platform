
using Microsoft.AspNetCore.SignalR;

namespace Realtime.Hubs;

public sealed class HubExceptionFilter : IHubFilter
{
    private readonly ILogger<HubExceptionFilter> _logger;

    public HubExceptionFilter(
        ILogger<HubExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> OnHubMethodInvocationAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (HubException)
        {
            // Preserve expected errors already prepared for the client.
            throw;
        }
        catch (OperationCanceledException)
            when (invocationContext.Context.ConnectionAborted
                .IsCancellationRequested)
        {
            // The client disconnected or cancelled the operation.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception in hub method {HubMethod}. " +
                "ConnectionId: {ConnectionId}.",
                invocationContext.HubMethodName,
                invocationContext.Context.ConnectionId);

            throw new HubException(
                "An unexpected error occurred. Please try again.");
        }
    }
}