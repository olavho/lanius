using Lanius.Business.Models;

namespace Lanius.Business.Services;

/// <summary>
/// Service for analyzing Git commits.
/// </summary>
public interface ICommitAnalyzer
{
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
    /// Get a specific commit by SHA.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="sha">The commit SHA.</param>
    /// <returns>The commit, or null if not found.</returns>
    Task<Commit?> GetCommitAsync(string repositoryId, string sha);

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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate diff statistics for a commit.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="sha">The commit SHA.</param>
    /// <returns>Diff statistics.</returns>
    Task<DiffStats?> GetCommitStatsAsync(string repositoryId, string sha);
}
