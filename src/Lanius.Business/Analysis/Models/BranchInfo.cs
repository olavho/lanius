namespace Lanius.Business.Analysis.Models;

/// <summary>
/// Branch information in the overview.
/// </summary>
public class BranchInfo
{
    public required string Name { get; init; }
    public required string HeadSha { get; init; }
    public required DateTimeOffset HeadTimestamp { get; init; }
}
