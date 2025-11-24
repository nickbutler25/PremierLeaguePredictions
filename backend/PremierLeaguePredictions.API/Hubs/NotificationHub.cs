using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace PremierLeaguePredictions.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private static readonly Dictionary<string, string> _userConnections = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            _userConnections[userId] = Context.ConnectionId;
            await base.OnConnectedAsync();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            _userConnections.Remove(userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public static string? GetConnectionId(string userId)
    {
        _userConnections.TryGetValue(userId, out var connectionId);
        return connectionId;
    }

    // Client subscribes to result updates
    public async Task SubscribeToResults()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ResultsUpdates");
    }

    // Client unsubscribes from result updates
    public async Task UnsubscribeFromResults()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "ResultsUpdates");
    }
}
