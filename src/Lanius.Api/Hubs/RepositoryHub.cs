using Lanius.Api.DTOs;
using Lanius.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace Lanius.Api.Hubs;

/// <summary>
/// SignalR hub for real-time repository updates and replay streaming.
/// </summary>
public class RepositoryHub : Hub
{
    private readonly ILogger<RepositoryHub> _logger;
    private readonly ReplaySignalRBridge _replayBridge;

    public RepositoryHub(
        ILogger<RepositoryHub> logger,
        ReplaySignalRBridge replayBridge)
    {
        _logger = logger;
        _replayBridge = replayBridge;
    }

    /// <summary>
    /// Client subscribes to updates for a specific repository.
    /// </summary>
    /// <param name="repositoryId">Repository ID to monitor.</param>
    public async Task SubscribeToRepository(string repositoryId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"repo:{repositoryId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to repository {RepositoryId}", 
            Context.ConnectionId, repositoryId);
    }

    /// <summary>
    /// Client unsubscribes from repository updates.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    public async Task UnsubscribeFromRepository(string repositoryId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"repo:{repositoryId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from repository {RepositoryId}", 
            Context.ConnectionId, repositoryId);
    }

    /// <summary>
    /// Client subscribes to a replay session stream.
    /// </summary>
    /// <param name="sessionId">Replay session ID.</param>
    public async Task SubscribeToReplay(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"replay:{sessionId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to replay session {SessionId}",
            Context.ConnectionId, sessionId);

        // Start streaming if not already started
        _replayBridge.StartStreaming(sessionId);
    }

    /// <summary>
    /// Client unsubscribes from a replay session.
    /// </summary>
    /// <param name="sessionId">Replay session ID.</param>
    public async Task UnsubscribeFromReplay(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"replay:{sessionId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from replay session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Server-side methods to broadcast to clients

    /// <summary>
    /// Broadcast new commits to all clients subscribed to a repository.
    /// </summary>
    public async Task BroadcastNewCommits(string repositoryId, IEnumerable<CommitResponse> commits)
    {
        await Clients.Group($"repo:{repositoryId}").SendAsync("ReceiveNewCommits", commits);
    }

    /// <summary>
    /// Broadcast repository update notification.
    /// </summary>
    public async Task BroadcastRepositoryUpdated(string repositoryId, RepositoryResponse repository)
    {
        await Clients.Group($"repo:{repositoryId}").SendAsync("RepositoryUpdated", repository);
    }

    /// <summary>
    /// Broadcast error to specific repository subscribers.
    /// </summary>
    public async Task BroadcastError(string repositoryId, string message)
    {
        await Clients.Group($"repo:{repositoryId}").SendAsync("Error", new { message, timestamp = DateTimeOffset.UtcNow });
    }
}
