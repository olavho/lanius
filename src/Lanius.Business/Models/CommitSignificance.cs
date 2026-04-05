namespace Lanius.Business.Models;

/// <summary>
/// Why a commit is significant.
/// </summary>
public enum CommitSignificance
{
    BranchHead,
    MergeBase,
    Both
}
