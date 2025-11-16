namespace Lanius.Business.Models;

/// <summary>
/// Simplified branch overview containing only significant commits.
/// </summary>
public class BranchOverview
{
    /// <summary>
    /// Branches included in the overview.
    /// </summary>
    public required List<BranchInfo> Branches { get; init; }

    /// <summary>
    /// Significant commits (branch heads and merge bases).
    /// </summary>
    public required List<SignificantCommitInfo> SignificantCommits { get; init; }

    /// <summary>
    /// Relationships between commits (merge bases).
    /// </summary>
    public required List<CommitRelation> Relationships { get; init; }
}

/// <summary>
/// Branch information in the overview.
/// </summary>
public class BranchInfo
{
    public required string Name { get; init; }
    public required string HeadSha { get; init; }
    public required DateTimeOffset HeadTimestamp { get; init; }
}

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

/// <summary>
/// Why a commit is significant.
/// </summary>
public enum CommitSignificance
{
    BranchHead,
    MergeBase,
    Both
}

/// <summary>
/// Relationship between two branches through a commit.
/// </summary>
public class CommitRelation
{
    public required string CommitSha { get; init; }
    public required string Branch1 { get; init; }
    public required string Branch2 { get; init; }
    public required CommitRelationType RelationType { get; init; }
}

/// <summary>
/// Type of relationship.
/// </summary>
public enum CommitRelationType
{
    MergeBase
}
