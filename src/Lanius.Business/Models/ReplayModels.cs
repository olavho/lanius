namespace Lanius.Business.Models;

/// <summary>
/// Configuration for commit replay playback.
/// </summary>
public record ReplayOptions
{
    /// <summary>
    /// Playback speed multiplier (1.0 = 1 commit per second).
    /// </summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>
    /// Optional start date for replay.
    /// </summary>
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>
    /// Optional end date for replay.
    /// </summary>
    public DateTimeOffset? EndDate { get; init; }

    /// <summary>
    /// Branch filter for replay.
    /// </summary>
    public string? BranchFilter { get; init; }
}

/// <summary>
/// State of a replay session.
/// </summary>
public enum ReplayState
{
    Idle,
    Playing,
    Paused,
    Completed,
    Cancelled
}

/// <summary>
/// Information about a replay session.
/// </summary>
public record ReplaySession
{
    public required string SessionId { get; init; }
    public required string RepositoryId { get; init; }
    public ReplayState State { get; init; }
    public required ReplayOptions Options { get; init; }
    public int TotalCommits { get; init; }
    public int CurrentIndex { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
