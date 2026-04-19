using System.Reactive.Linq;
using Lanius.Api.Hubs;
using Lanius.Business.Replay.Services;
using Microsoft.AspNetCore.SignalR;

namespace Lanius.Api.Services;

/// <summary>
/// Service that bridges replay sessions with SignalR for real-time streaming to clients.
/// </summary>
public class ReplaySignalRBridge(
    IReplayService replayService,
    IHubContext<RepositoryHub> hubContext,
    ILogger<ReplaySignalRBridge> logger)
{
    private readonly Dictionary<string, IDisposable> _subscriptions = [];

    /// <summary>
    /// Start streaming a replay session to SignalR clients.
    /// </summary>
    /// <param name="sessionId">Replay session ID.</param>
    public void StartStreaming(string sessionId)
    {
        var session = replayService.GetSession(sessionId);
        if (session == null)
        {
            logger.LogWarning("Cannot start streaming for non-existent session: {SessionId}", sessionId);
            return;
        }

        logger.LogInformation("Starting SignalR streaming for replay session: {SessionId}", sessionId);

        var stream = replayService.GetCommitStream(sessionId);

        var subscription = stream.Subscribe(
            onNext: commit =>
            {
                // Send SHA + sessionId so the frontend can discard stale events from old sessions
                hubContext.Clients.Group($"replay:{sessionId}")
                    .SendAsync("CommitRevealed", new { sha = commit.Sha, sessionId });

                logger.LogDebug("Revealed commit {Sha} for session {SessionId}", commit.Sha, sessionId);
            },
            onError: error =>
            {
                logger.LogError(error, "Error in replay stream for session: {SessionId}", sessionId);
                hubContext.Clients.Group($"replay:{sessionId}")
                    .SendAsync("ReplayError", new { sessionId, message = error.Message });

                CleanupSubscription(sessionId);
            },
            onCompleted: () =>
            {
                logger.LogInformation("Replay stream completed for session: {SessionId}", sessionId);
                hubContext.Clients.Group($"replay:{sessionId}")
                    .SendAsync("ReplayCompleted", new { sessionId });

                CleanupSubscription(sessionId);
            });

        lock (_subscriptions)
        {
            _subscriptions[sessionId] = subscription;
        }
    }

    /// <summary>
    /// Stop streaming a replay session.
    /// </summary>
    /// <param name="sessionId">Replay session ID.</param>
    public void StopStreaming(string sessionId)
    {
        logger.LogInformation("Stopping SignalR streaming for replay session: {SessionId}", sessionId);
        CleanupSubscription(sessionId);
    }

    private void CleanupSubscription(string sessionId)
    {
        lock (_subscriptions)
        {
            if (_subscriptions.TryGetValue(sessionId, out var subscription))
            {
                subscription.Dispose();
                _subscriptions.Remove(sessionId);
            }
        }
    }
}
