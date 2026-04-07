using Lanius.Business.Layout.Models;

namespace Lanius.Api.DTOs;

/// <summary>
/// Response containing calculated layout data for visualization.
/// </summary>
public class LayoutResponse
{
    /// <summary>
    /// Layout mode used for calculation.
    /// </summary>
    public required LayoutMode Mode { get; init; }

    /// <summary>
    /// Layout nodes (commits with positions).
    /// </summary>
    public required List<LayoutNodeDto> Nodes { get; init; }

    /// <summary>
    /// Layout edges (branch line connections).
    /// </summary>
    public required List<LayoutEdgeDto> Edges { get; init; }

    /// <summary>
    /// Total canvas width in pixels.
    /// </summary>
    public required double Width { get; init; }

    /// <summary>
    /// Total canvas height in pixels.
    /// </summary>
    public required double Height { get; init; }

    /// <summary>
    /// Earliest commit timestamp in the layout.
    /// </summary>
    public DateTimeOffset? MinTimestamp { get; init; }

    /// <summary>
    /// Latest commit timestamp in the layout.
    /// </summary>
    public DateTimeOffset? MaxTimestamp { get; init; }

    /// <summary>
    /// Total number of commits included in the layout.
    /// </summary>
    public int TotalCommits { get; init; }

    /// <summary>
    /// Total number of branches included in the layout.
    /// </summary>
    public int TotalBranches { get; init; }

    /// <summary>
    /// Number of branch rows in the grid.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Number of time columns in the grid.
    /// </summary>
    public int ColumnCount { get; init; }
}

/// <summary>
/// Layout node data transfer object.
/// </summary>
public class LayoutNodeDto
{
    /// <summary>
    /// Commit SHA.
    /// </summary>
    public required string CommitId { get; init; }

    /// <summary>
    /// X coordinate (horizontal position).
    /// </summary>
    public required double X { get; init; }

    /// <summary>
    /// Y coordinate (vertical position).
    /// </summary>
    public required double Y { get; init; }

    /// <summary>
    /// Node radius in pixels.
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
    public List<string> ParentShas { get; init; } = [];

    /// <summary>
    /// Whether this is a significant commit (merge, tag, etc.).
    /// </summary>
    public bool IsSignificant { get; init; }

    /// <summary>
    /// Zero-based grid row (branch lane index).
    /// </summary>
    public int GridRow { get; init; }

    /// <summary>
    /// Zero-based grid column (chronological commit index).
    /// </summary>
    public int GridColumn { get; init; }

    /// <summary>
    /// Ghost/shadow reference node — synthetic anchor, not rendered as a commit circle.
    /// </summary>
    public bool IsGhost { get; init; }
}

/// <summary>
/// Layout edge data transfer object.
/// </summary>
public class LayoutEdgeDto
{
    /// <summary>
    /// Source commit SHA.
    /// </summary>
    public required string FromCommitId { get; init; }

    /// <summary>
    /// Target commit SHA.
    /// </summary>
    public required string ToCommitId { get; init; }

    /// <summary>
    /// Edge type (normal branch line, merge, split).
    /// </summary>
    public required EdgeType Type { get; init; }

    /// <summary>
    /// Branch name for the edge.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Start X coordinate.
    /// </summary>
    public required double X1 { get; init; }

    /// <summary>
    /// Start Y coordinate.
    /// </summary>
    public required double Y1 { get; init; }

    /// <summary>
    /// End X coordinate.
    /// </summary>
    public required double X2 { get; init; }

    /// <summary>
    /// End Y coordinate.
    /// </summary>
    public required double Y2 { get; init; }

    /// <summary>
    /// Whether this edge is vertical (X1 == X2).
    /// </summary>
    public bool IsVertical { get; init; }

    /// <summary>
    /// Direction of the edge: Horizontal, Upward, or Downward.
    /// </summary>
    public required string Direction { get; init; }
}
