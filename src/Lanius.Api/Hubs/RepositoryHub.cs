using Lanius.Api.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Lanius.Api.Hubs;

/// <summary>
/// SignalR hub for real-time repository updates.
/// </summary>
public class RepositoryHub : Hub
{
    private readonly ILogger<RepositoryHub> _logger;

    public RepositoryHub(ILogger<RepositoryHub> logger)
    {
        _logger = logger;
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
