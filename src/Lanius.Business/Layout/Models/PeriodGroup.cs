namespace Lanius.Business.Layout.Models;

/// <summary>
/// Represents a time period group of commits for calendar layout.
/// </summary>
public record PeriodGroup
{
    public required DateTimeOffset PeriodStart { get; init; }
    public required DateTimeOffset PeriodEnd { get; init; }
    public required int CommitCount { get; init; }
    public required List<string> CommitIds { get; init; }
}
