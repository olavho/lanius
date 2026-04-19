namespace Lanius.Business.Layout.Models;

/// <summary>
/// Represents a branch with hierarchy information for efficient loading.
/// </summary>
public record BranchHierarchyInfo
{
    /// <summary>
    /// Branch name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Hierarchy tier (Main, Project, Release, Feature, Other)
    /// </summary>
    public BranchTier Tier { get; init; }

    /// <summary>
    /// Parent branch name (for finding merge base)
    /// </summary>
    public string? ParentBranchName { get; init; }

    /// <summary>
    /// SHA of merge base commit (branch point from parent)
    /// </summary>
    public string? MergeBaseSha { get; init; }

    /// <summary>
    /// Timestamp of the merge base commit (when this branch split from its parent)
    /// </summary>
    public DateTimeOffset? SplitTimestamp { get; init; }

    /// <summary>
    /// Number of commits on this branch since merge base
    /// </summary>
    public int CommitCount { get; init; }
}
