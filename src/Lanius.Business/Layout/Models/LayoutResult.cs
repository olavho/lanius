namespace Lanius.Business.Layout.Models;

/// <summary>
/// Result of layout calculation containing positioned nodes and edges.
/// </summary>
public record LayoutResult
{
    /// <summary>
    /// Layout mode used for calculation.
    /// </summary>
    public required LayoutMode Mode { get; init; }

    /// <summary>
    /// Positioned commit nodes.
    /// </summary>
    public required List<LayoutNode> Nodes { get; init; }

    /// <summary>
    /// Edges connecting commits (branch lines).
    /// </summary>
    public required List<LayoutEdge> Edges { get; init; }

    /// <summary>
    /// Calculated canvas width (may exceed requested width).
    /// </summary>
    public required double Width { get; init; }

    /// <summary>
    /// Calculated canvas height (may exceed requested height).
    /// </summary>
    public required double Height { get; init; }

    /// <summary>
    /// Time range covered by layout.
    /// </summary>
    public DateTimeOffset? MinTimestamp { get; init; }

    /// <summary>
    /// Time range covered by layout.
    /// </summary>
    public DateTimeOffset? MaxTimestamp { get; init; }

    /// <summary>
    /// X-pixel position corresponding to MinTimestamp (Timeline mode only).
    /// Allows the frontend to reconstruct the exact same time→x mapping used by the engine.
    /// </summary>
    public double? TimelineOriginX { get; init; }

    /// <summary>
    /// Pixels per second along the time axis (Timeline mode only).
    /// </summary>
    public double? TimelinePixelsPerSecond { get; init; }

    /// <summary>
    /// Total number of commits included in layout.
    /// </summary>
    public int TotalCommits { get; init; }

    /// <summary>
    /// Number of branches included in layout.
    /// </summary>
    public int TotalBranches { get; init; }

    /// <summary>
    /// Number of branch rows in the grid.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Number of time columns in the grid (equals the highest GridColumn + 1).
    /// </summary>
    public int ColumnCount { get; init; }
}
