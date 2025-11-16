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
    /// Get branches matching a filter pattern (e.g., "main", "release/*").
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="patterns">Branch name patterns (supports wildcards).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered list of branches.</returns>
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
    /// Calculate divergence between two branches (commits ahead/behind).
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
}
