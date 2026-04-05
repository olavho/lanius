namespace Lanius.Business.Replay.Models;

/// <summary>
/// Information about a replay session.
/// </summary>
public record ReplaySession
{
    public required string SessionId { get; init; }
    public required string RepositoryId { get; init; }
    public ReplayState State { get; init; }
    public required ReplayOptions Options { get; init; }
    public int TotalCommits { get; init; }
    public int CurrentIndex { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
