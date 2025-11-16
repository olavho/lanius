namespace Lanius.Business.Models;

/// <summary>
/// Represents a Git branch with metadata.
/// </summary>
public class Branch
{
    /// <summary>
    /// The branch name (e.g., "main", "feature/auth").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The full reference name (e.g., "refs/heads/main").
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// SHA of the commit this branch points to.
    /// </summary>
    public required string TipSha { get; init; }

    /// <summary>
    /// Whether this is a remote branch.
    /// </summary>
    public bool IsRemote { get; init; }

    /// <summary>
    /// The remote name (e.g., "origin"), if remote branch.
    /// </summary>
    public string? RemoteName { get; init; }

    /// <summary>
    /// Whether this branch is currently checked out.
    /// </summary>
    public bool IsHead { get; init; }

    /// <summary>
    /// Upstream branch name, if configured.
    /// </summary>
    public string? UpstreamBranch { get; init; }

    /// <summary>
    /// Number of commits ahead of upstream.
    /// </summary>
    public int? CommitsAhead { get; init; }

    /// <summary>
    /// Number of commits behind upstream.
    /// </summary>
    public int? CommitsBehind { get; init; }

    /// <summary>
    /// Common ancestor SHA with another branch (used for divergence analysis).
    /// </summary>
    public string? CommonAncestorSha { get; init; }
}
