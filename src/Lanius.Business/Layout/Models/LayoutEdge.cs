namespace Lanius.Business.Layout.Models;

/// <summary>
/// Edge types for connections between commits.
/// </summary>
public enum EdgeType
{
    /// <summary>
    /// Normal commit-to-commit connection on same branch.
    /// </summary>
    Normal,

    /// <summary>
    /// Branch split (divergence from parent branch).
    /// </summary>
    Branch,

    /// <summary>
    /// Merge connection (multiple commits merging into one).
    /// </summary>
    Merge
}

/// <summary>
/// Direction of a cross-branch edge.
/// </summary>
public enum EdgeDirection
{
    /// <summary>
    /// Edge runs horizontally (normal within-branch connection).
    /// </summary>
    Horizontal,

    /// <summary>
    /// Edge goes upward (decreasing Y) — merge into a higher branch lane.
    /// </summary>
    Upward,

    /// <summary>
    /// Edge goes downward (increasing Y) — split to a lower branch lane.
    /// </summary>
    Downward
}

/// <summary>
/// Represents a connection between commits (branch line segment).
/// </summary>
public record LayoutEdge
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
    /// Type of connection (Normal, Branch, Merge).
    /// </summary>
    public required EdgeType Type { get; init; }

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
    /// Branch this edge belongs to.
    /// </summary>
    public string? BranchName { get; init; }

    /// <summary>
    /// Whether this edge is vertical (X1 == X2); true for Branch and Merge type edges.
    /// </summary>
    public bool IsVertical { get; init; }

    /// <summary>
    /// Direction of travel for cross-branch edges.
    /// </summary>
    public EdgeDirection Direction { get; init; } = EdgeDirection.Horizontal;

    /// <summary>
    /// True when the time gap between the two endpoints exceeds 14 days.
    /// The frontend can render long-span edges as dashed to indicate the gap.
    /// </summary>
    public bool IsLongSpan { get; init; }
}
