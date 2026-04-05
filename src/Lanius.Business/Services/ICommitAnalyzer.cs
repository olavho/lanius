using Lanius.Business.Models;

namespace Lanius.Business.Services;

/// <summary>
/// Service for analyzing Git commits.
/// </summary>
public interface ICommitAnalyzer
{

    /// <summary>
    /// Get a specific commit by SHA.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="sha">The commit SHA.</param>
    /// <returns>The commit, or null if not found.</returns>
    Task<Commit?> GetCommitAsync(string repositoryId, string sha);
    /// <summary>
    /// Get all commits from a repository.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="branchName">Optional branch filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of commits.</returns>

    Task<IReadOnlyList<Commit>> GetCommitsAsync(
        string repositoryId,
        string? branchName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get commits from a branch since a specific commit (merge base).
    /// Optimized for branch visualization - only loads commits after branch point.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="branchName">Branch name.</param>
    /// <param name="sinceCommitSha">SHA of commit to exclude (merge base). If null, returns all commits.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of commits since the specified commit.</returns>
    Task<IReadOnlyList<Commit>> GetCommitsSinceAsync(
        string repositoryId,
        string branchName,
        string? sinceCommitSha = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch load commits for multiple branches with a single repository open.
    /// Avoids expensive repository open/close cycles when loading many branches.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="branches">Branch names paired with their merge-base SHA (null = all commits).</param>
    /// <param name="progress">Optional per-branch progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of branch name to commits since the merge base.</returns>
    Task<Dictionary<string, IReadOnlyList<Commit>>> GetCommitsBatchAsync(
        string repositoryId,
        IReadOnlyList<(string branchName, string? sinceCommitSha)> branches,
        IProgress<(int processed, int total, string currentBranch)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get commits in chronological order (for replay mode).
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Commits ordered by timestamp.</returns>
    Task<IReadOnlyList<Commit>> GetCommitsChronologicallyAsync(
        string repositoryId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default,
        IProgress<(int processed, int total)>? progress = null);

    /// <summary>
    /// Calculate diff statistics for a commit.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="sha">The commit SHA.</param>
    /// <returns>Diff statistics.</returns>
    Task<DiffStats?> GetCommitStatsAsync(string repositoryId, string sha);
}
