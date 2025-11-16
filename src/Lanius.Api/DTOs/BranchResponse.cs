namespace Lanius.Api.DTOs;

/// <summary>
/// Response containing branch information.
/// </summary>
public class BranchResponse
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string TipSha { get; init; }
    public bool IsRemote { get; init; }
    public string? RemoteName { get; init; }
    public bool IsHead { get; init; }
    public string? UpstreamBranch { get; init; }
    public int? CommitsAhead { get; init; }
    public int? CommitsBehind { get; init; }
}
