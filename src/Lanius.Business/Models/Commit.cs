namespace Lanius.Business.Models;

/// <summary>
/// Represents a Git commit with metadata and statistics.
/// </summary>
public class Commit
{
    /// <summary>
    /// The SHA hash of the commit.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// The commit author name.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// The commit author email.
    /// </summary>
    public required string AuthorEmail { get; init; }

    /// <summary>
    /// When the commit was authored.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The commit message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Short commit message (first line).
    /// </summary>
    public string ShortMessage => Message.Split('\n', 2)[0];

    /// <summary>
    /// Parent commit SHAs (empty for initial commit, multiple for merges).
    /// </summary>
    public required IReadOnlyList<string> ParentShas { get; init; }

    /// <summary>
    /// Whether this is a merge commit.
    /// </summary>
    public bool IsMerge => ParentShas.Count > 1;

    /// <summary>
    /// Diff statistics for this commit.
    /// </summary>
    public DiffStats? Stats { get; init; }

    /// <summary>
    /// Branch names that include this commit.
    /// </summary>
    public IReadOnlyList<string> Branches { get; init; } = Array.Empty<string>();
}
