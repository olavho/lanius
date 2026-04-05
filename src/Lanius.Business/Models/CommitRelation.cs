namespace Lanius.Business.Models;

/// <summary>
/// Relationship between two branches through a commit.
/// </summary>
public class CommitRelation
{
    public required string CommitSha { get; init; }
    public required string Branch1 { get; init; }
    public required string Branch2 { get; init; }
    public required CommitRelationType RelationType { get; init; }
}
