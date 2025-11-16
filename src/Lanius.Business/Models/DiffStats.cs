namespace Lanius.Business.Models;

/// <summary>
/// Statistics about lines added and removed in a commit.
/// </summary>
public class DiffStats
{
    /// <summary>
    /// Number of lines added.
    /// </summary>
    public int LinesAdded { get; init; }

    /// <summary>
    /// Number of lines removed.
    /// </summary>
    public int LinesRemoved { get; init; }

    /// <summary>
    /// Total lines changed (added + removed).
    /// </summary>
    public int TotalChanges => LinesAdded + LinesRemoved;

    /// <summary>
    /// Net change in lines (added - removed).
    /// </summary>
    public int NetChange => LinesAdded - LinesRemoved;

    /// <summary>
    /// Number of files changed.
    /// </summary>
    public int FilesChanged { get; init; }

    /// <summary>
    /// Calculate color indicator for visualization (-1 = red/deletions, 0 = neutral, 1 = green/additions).
    /// </summary>
    public double ColorIndicator
    {
        get
        {
            if (TotalChanges == 0) return 0;
            return (double)NetChange / TotalChanges;
        }
    }
}
