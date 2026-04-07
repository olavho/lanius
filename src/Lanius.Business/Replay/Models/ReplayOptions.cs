namespace Lanius.Business.Replay.Models;

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

    /// <summary>
    /// Zero-based index of the first commit to stream (used for scrub/seek).
    /// </summary>
    public int StartIndex { get; init; } = 0;

    /// <summary>
    /// When true and BranchFilter is a non-main branch, replay starts from the
    /// branch split point (merge base) rather than the very first repository commit.
    /// </summary>
    public bool StartFromBranchSplit { get; init; } = false;
}
