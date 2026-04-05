namespace Lanius.Business.Layout.Models;

/// <summary>
/// Options for layout calculation.
/// </summary>
public record LayoutOptions
{
    /// <summary>
    /// Layout mode (Logical or Calendar).
    /// </summary>
    public LayoutMode Mode { get; init; } = LayoutMode.Logical;

    /// <summary>
    /// Calendar granularity (only used when Mode is Calendar). Null means auto-detect.
    /// </summary>
    public CalendarGranularity? Granularity { get; init; } = null;

    /// <summary>
    /// Current zoom level (0.1 to 10.0).
    /// </summary>
    public double ZoomLevel { get; init; } = 1.0;

    /// <summary>
    /// Branch filter (comma-separated branch names). Null/empty means all branches.
    /// </summary>
    public string? BranchFilter { get; init; }

    /// <summary>
    /// Width of the canvas in pixels.
    /// </summary>
    public double CanvasWidth { get; init; } = 1200;

    /// <summary>
    /// Height of the canvas in pixels.
    /// </summary>
    public double CanvasHeight { get; init; } = 600;

    /// <summary>
    /// Spacing between branch lanes (pixels).
    /// </summary>
    public double BranchSpacing { get; init; } = 40;

    /// <summary>
    /// Horizontal margin (pixels).
    /// </summary>
    public double MarginX { get; init; } = 100;

    /// <summary>
    /// Vertical margin (pixels).
    /// </summary>
    public double MarginY { get; init; } = 60;
}
