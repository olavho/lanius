using System.Reactive.Linq;
using Lanius.Api.DTOs;
using Lanius.Api.Hubs;
using Lanius.Business.Services;
using Microsoft.AspNetCore.SignalR;

namespace Lanius.Api.Services;

/// <summary>
/// Service that bridges replay sessions with SignalR for real-time streaming to clients.
/// </summary>
public class ReplaySignalRBridge
{
    private readonly IReplayService _replayService;
    private readonly IHubContext<RepositoryHub> _hubContext;
    private readonly ILogger<ReplaySignalRBridge> _logger;
    private readonly Dictionary<string, IDisposable> _subscriptions = new();

    public ReplaySignalRBridge(
        IReplayService replayService,
        IHubContext<RepositoryHub> hubContext,
        ILogger<ReplaySignalRBridge> logger)
    {
        _replayService = replayService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Start streaming a replay session to SignalR clients.
    /// </summary>
    /// <param name="sessionId">Replay session ID.</param>
    public void StartStreaming(string sessionId)
    {
        var session = _replayService.GetSession(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Cannot start streaming for non-existent session: {SessionId}", sessionId);
            return;
        }

        _logger.LogInformation("Starting SignalR streaming for replay session: {SessionId}", sessionId);

        var stream = _replayService.GetCommitStream(sessionId);

        var subscription = stream.Subscribe(
            onNext: commit =>
            {
                // Convert to DTO
                var commitResponse = new CommitResponse
                {
                    Sha = commit.Sha,
                    Author = commit.Author,
                    AuthorEmail = commit.AuthorEmail,
                    Timestamp = commit.Timestamp,
                    Message = commit.Message,
                    ShortMessage = commit.ShortMessage,
                    ParentShas = commit.ParentShas.ToList(),
                    IsMerge = commit.IsMerge,
                    Stats = commit.Stats != null ? new DiffStatsResponse
                    {
                        LinesAdded = commit.Stats.LinesAdded,
                        LinesRemoved = commit.Stats.LinesRemoved,
                        TotalChanges = commit.Stats.TotalChanges,
                        NetChange = commit.Stats.NetChange,
                        FilesChanged = commit.Stats.FilesChanged,
                        ColorIndicator = commit.Stats.ColorIndicator
                    } : null,
                    Branches = commit.Branches.ToList()
                };

                // Broadcast to replay group
                _hubContext.Clients.Group($"replay:{sessionId}")
                    .SendAsync("ReplayCommit", commitResponse);

                _logger.LogDebug("Streamed commit {Sha} for session {SessionId}", commit.Sha, sessionId);
            },
            onError: error =>
            {
                _logger.LogError(error, "Error in replay stream for session: {SessionId}", sessionId);
                _hubContext.Clients.Group($"replay:{sessionId}")
                    .SendAsync("ReplayError", new { sessionId, message = error.Message });
                
                CleanupSubscription(sessionId);
            },
            onCompleted: () =>
            {
                _logger.LogInformation("Replay stream completed for session: {SessionId}", sessionId);
                _hubContext.Clients.Group($"replay:{sessionId}")
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
        _logger.LogInformation("Stopping SignalR streaming for replay session: {SessionId}", sessionId);
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
