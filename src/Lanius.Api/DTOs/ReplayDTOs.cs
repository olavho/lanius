namespace Lanius.Api.DTOs;

/// <summary>
/// Request to start a replay session.
/// </summary>
public class StartReplayRequest
{
    /// <summary>
    /// Playback speed multiplier (1.0 = 1 commit per second).
    /// Default: 1.0
    /// </summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>
    /// Optional start date for replay filter.
    /// </summary>
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>
    /// Optional end date for replay filter.
    /// </summary>
    public DateTimeOffset? EndDate { get; init; }

    /// <summary>
    /// Optional branch filter.
    /// </summary>
    public string? BranchFilter { get; init; }
}

/// <summary>
/// Response containing replay session information.
/// </summary>
public class ReplaySessionResponse
{
    public required string SessionId { get; init; }
    public required string RepositoryId { get; init; }
    public required string State { get; init; }
    public double Speed { get; init; }
    public int TotalCommits { get; init; }
    public int CurrentIndex { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
