using Lanius.Business.Storage.Models;

namespace Lanius.Business.Storage.Services;

/// <summary>
/// Service for Git repository storage operations (clone, fetch, delete, list).
/// </summary>
public interface IRepositoryStorageService
{
    /// <summary>
    /// Clone a repository from a URL.
    /// </summary>
    /// <param name="url">The Git repository URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Repository information.</returns>
    Task<RepositoryInfo> CloneRepositoryAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch updates from remote repository.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if new commits were fetched.</returns>
    Task<bool> FetchUpdatesAsync(string repositoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get repository information.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <returns>Repository information.</returns>
    Task<RepositoryInfo?> GetRepositoryInfoAsync(string repositoryId);

    /// <summary>
    /// Check if repository exists locally.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <returns>True if repository exists.</returns>
    bool RepositoryExists(string repositoryId);

    /// <summary>
    /// Delete a repository from disk.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    Task DeleteRepositoryAsync(string repositoryId);

    /// <summary>
    /// List all locally cloned repositories.
    /// </summary>
    /// <returns>List of repository information.</returns>
    Task<IEnumerable<RepositoryInfo>> ListRepositoriesAsync();

    /// <summary>
    /// Get the local file system path for a repository.
    /// </summary>
    /// <param name="repositoryId">The repository ID.</param>
    /// <returns>Absolute local path to the repository directory.</returns>
    string GetRepositoryPath(string repositoryId);
}
