namespace Lanius.Api.DTOs;

/// <summary>
/// Simplified branch overview showing only significant commits (heads and merge bases).
/// </summary>
public class BranchOverviewResponse
{
    /// <summary>
    /// Branches included in the overview.
    /// </summary>
    public required List<BranchSummary> Branches { get; init; }

    /// <summary>
    /// Significant commits (branch heads and merge bases).
    /// </summary>
    public required List<SignificantCommit> SignificantCommits { get; init; }

    /// <summary>
    /// Relationships between commits (which commits are merge bases between which branches).
    /// </summary>
    public required List<CommitRelationship> Relationships { get; init; }
}

/// <summary>
/// Summary information about a branch.
/// </summary>
public class BranchSummary
{
    public required string Name { get; init; }
    public required string HeadSha { get; init; }
    public required DateTimeOffset HeadTimestamp { get; init; }
}

/// <summary>
/// A significant commit (branch head or merge base).
/// </summary>
public class SignificantCommit
{
    public required string Sha { get; init; }
    public required string Author { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ShortMessage { get; init; }
    public required List<string> Branches { get; init; }
    public SignificantCommitType Type { get; init; }
    public DiffStatsResponse? Stats { get; init; }
}

/// <summary>
/// Type of significant commit.
/// </summary>
public enum SignificantCommitType
{
    BranchHead,
    MergeBase,
    Both
}

/// <summary>
/// Relationship between commits (e.g., merge base between two branches).
/// </summary>
public class CommitRelationship
{
    public required string CommitSha { get; init; }
    public required string Branch1 { get; init; }
    public required string Branch2 { get; init; }
    public required RelationshipType Type { get; init; }
}

/// <summary>
/// Type of relationship between commits.
/// </summary>
public enum RelationshipType
{
    MergeBase,
    DirectAncestor
}
