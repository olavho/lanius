using Lanius.Business.Models;

namespace Lanius.Business.Services;

/// <summary>
/// Service for replaying commit history with Rx.NET observables.
/// </summary>
public interface IReplayService
{
    /// <summary>
    /// Start a new replay session.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="options">Replay options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session information.</returns>
    Task<ReplaySession> StartReplayAsync(string repositoryId, ReplayOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause a replay session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    void PauseReplay(string sessionId);

    /// <summary>
    /// Resume a paused replay session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    void ResumeReplay(string sessionId);

    /// <summary>
    /// Stop and cancel a replay session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    void StopReplay(string sessionId);

    /// <summary>
    /// Adjust playback speed of a running session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="speed">New speed multiplier.</param>
    void SetSpeed(string sessionId, double speed);

    /// <summary>
    /// Get replay session information.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Session information or null if not found.</returns>
    ReplaySession? GetSession(string sessionId);

    /// <summary>
    /// Get observable stream of commits for a session.
    /// Subscribe to receive commits as they are "replayed".
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Observable stream of commits.</returns>
    IObservable<Commit> GetCommitStream(string sessionId);
}
