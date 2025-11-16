using Lanius.Business.Models;

namespace Lanius.Business.Services;

/// <summary>
/// Service for analyzing Git branches.
/// </summary>
public interface IBranchAnalyzer
{
    /// <summary>
    /// Get all branches from a repository.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="includeRemote">Whether to include remote branches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of branches.</returns>
    Task<IReadOnlyList<Branch>> GetBranchesAsync(
        string repositoryId,
        bool includeRemote = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get branches matching specific patterns.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="patterns">Branch name patterns (supports wildcards like "release/*").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching branches.</returns>
    Task<IReadOnlyList<Branch>> GetBranchesByPatternAsync(
        string repositoryId,
        IEnumerable<string> patterns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific branch by name.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="branchName">The branch name.</param>
    /// <returns>The branch, or null if not found.</returns>
    Task<Branch?> GetBranchAsync(string repositoryId, string branchName);

    /// <summary>
    /// Calculate how far ahead/behind one branch is from another.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="baseBranch">The base branch name.</param>
    /// <param name="compareBranch">The branch to compare.</param>
    /// <returns>Tuple of (commitsAhead, commitsBehind).</returns>
    Task<(int ahead, int behind)> GetBranchDivergenceAsync(
        string repositoryId,
        string baseBranch,
        string compareBranch);

    /// <summary>
    /// Find the common ancestor commit between two branches.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="branch1">First branch name.</param>
    /// <param name="branch2">Second branch name.</param>
    /// <returns>The common ancestor commit SHA, or null if not found.</returns>
    Task<string?> FindCommonAncestorAsync(
        string repositoryId,
        string branch1,
        string branch2);

    /// <summary>
    /// Get a simplified branch overview with only significant commits (branch heads and merge bases).
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="branchNames">List of branch names to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Branch overview with significant commits.</returns>
    Task<BranchOverview> GetBranchOverviewAsync(
        string repositoryId,
        IEnumerable<string> branchNames,
        CancellationToken cancellationToken = default);
}
