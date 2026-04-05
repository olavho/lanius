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
    /// Whether this is a significant commit (merge, tag, etc.).
    /// </summary>
    public bool IsSignificant { get; init; }
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
    /// Polyline points for rendering curved/segmented edges.
    /// </summary>
    public List<(double X, double Y)>? Points { get; init; }
}
