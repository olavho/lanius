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
}
