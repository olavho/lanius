namespace Lanius.Api.DTOs;

/// <summary>
/// Response containing commit information.
/// </summary>
public class CommitResponse
{
    public required string Sha { get; init; }
    public required string Author { get; init; }
    public required string AuthorEmail { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Message { get; init; }
    public required string ShortMessage { get; init; }
    public required List<string> ParentShas { get; init; }
    public bool IsMerge { get; init; }
    public DiffStatsResponse? Stats { get; init; }
    public required List<string> Branches { get; init; }
}

/// <summary>
/// Diff statistics for a commit.
/// </summary>
public class DiffStatsResponse
{
    public int LinesAdded { get; init; }
    public int LinesRemoved { get; init; }
    public int TotalChanges { get; init; }
    public int NetChange { get; init; }
    public int FilesChanged { get; init; }
    public double ColorIndicator { get; init; }
}
