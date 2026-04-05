namespace Lanius.Business.Layout.Models;

/// <summary>
/// Defines the layout mode for visualizing repository commits.
/// </summary>
public enum LayoutMode
{
    /// <summary>
    /// Logical layout showing all commits on branch lines with split/merge points.
    /// </summary>
    Logical,

    /// <summary>
    /// Calendar-based layout grouping commits by time periods.
    /// </summary>
    Calendar
}
