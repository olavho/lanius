namespace Lanius.Business.Layout.Models;

/// <summary>
/// Represents a commit positioned in 2D space for rendering.
/// </summary>
public record LayoutNode
{
    /// <summary>
    /// Commit SHA identifier.
    /// </summary>
    public required string CommitId { get; init; }

    /// <summary>
    /// X position (typically based on timestamp).
    /// </summary>
    public required double X { get; init; }

    /// <summary>
    /// Y position (typically based on branch lane).
    /// </summary>
    public required double Y { get; init; }

    /// <summary>
    /// Radius for rendering the commit dot.
    /// </summary>
    public required double Radius { get; init; }

    /// <summary>
    /// Branch name this commit belongs to.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Commit timestamp.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Commit message (first line).
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Commit author name.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Commit author email.
    /// </summary>
    public string? AuthorEmail { get; init; }

    /// <summary>
    /// Committer name.
    /// </summary>
    public string? Committer { get; init; }

    /// <summary>
    /// Committer email.
    /// </summary>
    public string? CommitterEmail { get; init; }

    /// <summary>
    /// When the commit was committed.
    /// </summary>
    public DateTimeOffset? CommitterTimestamp { get; init; }

    /// <summary>
    /// Parent commit SHAs.
    /// </summary>
    public IReadOnlyList<string> ParentShas { get; init; } = [];

    /// <summary>
    /// Is this a significant commit (merge, split, branch head)?
    /// </summary>
    public bool IsSignificant { get; init; }

    /// <summary>
    /// Zero-based grid row (branch lane index).
    /// </summary>
    public int GridRow { get; init; }

    /// <summary>
    /// Zero-based grid column (chronological commit index with per-row collision avoidance).
    /// </summary>
    public int GridColumn { get; init; }

    /// <summary>
    /// Ghost/shadow reference node — synthetic anchor for split edges. Not rendered as a commit circle.
    /// </summary>
    public bool IsGhost { get; init; }

    /// <summary>
    /// Number of commits in this calendar group (0 for individual commit nodes).
    /// </summary>
    public int CommitCount { get; init; }

    /// <summary>
    /// Formatted commit listing lines for calendar groups ("sha8  date  title").
    /// </summary>
    public List<string> GroupCommitLines { get; init; } = [];
}
