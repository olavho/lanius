namespace Lanius.Business.Models;

/// <summary>
/// Information about a Git repository.
/// </summary>
public class RepositoryInfo
{
    /// <summary>
    /// Unique identifier for this repository instance.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The original clone URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Local path where repository is stored.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    /// Name of the default branch (e.g., "main", "master").
    /// </summary>
    public required string DefaultBranch { get; init; }

    /// <summary>
    /// When the repository was cloned.
    /// </summary>
    public required DateTimeOffset ClonedAt { get; init; }

    /// <summary>
    /// When the repository was last fetched/updated.
    /// </summary>
    public DateTimeOffset? LastFetchedAt { get; init; }

    /// <summary>
    /// Total number of commits in the repository.
    /// </summary>
    public int TotalCommits { get; init; }

    /// <summary>
    /// Total number of branches.
    /// </summary>
    public int TotalBranches { get; init; }
}
