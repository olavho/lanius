namespace Lanius.Api.DTOs;

/// <summary>
/// Response containing repository information.
/// </summary>
public class RepositoryResponse
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string DefaultBranch { get; init; }
    public required DateTimeOffset ClonedAt { get; init; }
    public DateTimeOffset? LastFetchedAt { get; init; }
    public int TotalCommits { get; init; }
    public int TotalBranches { get; init; }
    public bool AlreadyExisted { get; init; }
}
