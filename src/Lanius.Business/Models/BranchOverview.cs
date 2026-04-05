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
