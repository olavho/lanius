namespace Lanius.Business.Analysis.Models;

/// <summary>
/// A significant commit in the branch overview.
/// </summary>
public class SignificantCommitInfo
{
    public required string Sha { get; init; }
    public required string Author { get; init; }
    public required string AuthorEmail { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Message { get; init; }
    public required string ShortMessage { get; init; }
    public required List<string> Branches { get; set; } // Changed to set for mutation
    public required CommitSignificance Significance { get; set; } // Changed to set for mutation
    public DiffStats? Stats { get; init; }
}
