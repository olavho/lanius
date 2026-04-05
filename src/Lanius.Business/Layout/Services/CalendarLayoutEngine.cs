using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Microsoft.Extensions.Logging;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Calendar-based layout engine that groups commits by time periods.
/// Phase 4a MVP: Month granularity, single horizontal row.
/// </summary>
public class CalendarLayoutEngine(
    ICommitAnalyzer commitAnalyzer,
    IBranchAnalyzer branchAnalyzer,
    ILogger<CalendarLayoutEngine> logger) : ILayoutEngine
{
    private const int MinRadius = 4;
    private const int MaxRadius = 20;
    private const int CenterY = 300; // Single horizontal row

    public async Task<LayoutResult> CalculateLayoutAsync(
        string repositoryId,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        progress?.Report(new LayoutProgress(0, "Loading commits...", 0, 0));

        // Load all commits (calendar view shows all branches aggregated)
        var loadProgress = new Progress<(int processed, int total)>(p =>
        {
            if (p.total == 0) return;
            var pct = (int)((p.processed / (double)p.total) * 30);
            progress?.Report(new LayoutProgress(pct, $"Loading commits ({p.processed}/{p.total})...", p.processed, p.total));
        });
        var allCommits = await commitAnalyzer.GetCommitsChronologicallyAsync(
            repositoryId,
            cancellationToken: cancellationToken,
            progress: loadProgress);

        if (allCommits.Count == 0)
        {
            logger.LogInformation("No commits found for repository {RepositoryId}", repositoryId);
            return new LayoutResult
            {
                Mode = LayoutMode.Calendar,
                Nodes = [],
                Edges = [],
                Width = options.CanvasWidth,
                Height = options.CanvasHeight,
                TotalCommits = 0,
                TotalBranches = 0
            };
        }

        // Determine granularity (auto or manual)
        var granularity = DetermineGranularity(allCommits, options.Granularity);

        progress?.Report(new LayoutProgress(30, $"Grouping commits by {granularity}...", 0, allCommits.Count));

        logger.LogInformation(
            "Calendar layout for {CommitCount} commits with {Granularity} granularity",
            allCommits.Count, granularity);

        var groups = granularity switch
        {
            CalendarGranularity.Day => GroupCommitsByDay(allCommits),
            CalendarGranularity.Week => GroupCommitsByWeek(allCommits),
            CalendarGranularity.Year => GroupCommitsByYear(allCommits),
            _ => GroupCommitsByMonth(allCommits)
        };

        progress?.Report(new LayoutProgress(60, "Calculating layout positions...", 0, groups.Count));

        // Calculate node positions
        var nodes = CalculateNodePositions(groups, (int)options.CanvasWidth, granularity);

        progress?.Report(new LayoutProgress(100, "Layout complete", groups.Count, groups.Count));

        var result = new LayoutResult
        {
            Mode = LayoutMode.Calendar,
            Nodes = nodes,
            Edges = [], // No edges in calendar view (Phase 4a)
            Width = options.CanvasWidth,
            Height = options.CanvasHeight,
            TotalCommits = allCommits.Count,
            TotalBranches = allCommits.SelectMany(c => c.Branches).Distinct().Count()
        };

        logger.LogInformation(
            "Calendar layout complete: {NodeCount} nodes for {CommitCount} commits",
            nodes.Count, allCommits.Count);

        return result;
    }

    /// <summary>
    /// Determine granularity based on date range and option override.
    /// </summary>
    private static CalendarGranularity DetermineGranularity(
        IReadOnlyList<Commit> commits,
        CalendarGranularity? granularityOverride)
    {
        // Manual override takes precedence
        if (granularityOverride.HasValue)
        {
            return granularityOverride.Value;
        }

        // Auto-detect based on date range
        var minDate = commits.Min(c => c.Timestamp);
        var maxDate = commits.Max(c => c.Timestamp);
        var span = maxDate - minDate;

        return span.TotalDays switch
        {
            < 150 => CalendarGranularity.Day,      // < 150 days = daily (1-150 columns)
            < 600 => CalendarGranularity.Week,     // 150-600 days = weekly (~21-86 columns)
            < 1825 => CalendarGranularity.Month,   // 1.6-5 years = monthly (~20-60 columns)
            _ => CalendarGranularity.Year          // > 5 years = yearly
        };
    }

    /// <summary>
    /// Group commits by month using commit date (Committer.When).
    /// Phase 4a MVP: Month granularity only.
    /// </summary>
    public List<PeriodGroup> GroupCommitsByMonth(IReadOnlyList<Commit> commits)
    {
        var groups = commits
            .GroupBy(c => new DateTime(c.Timestamp.Year, c.Timestamp.Month, 1)) // Month start
            .OrderBy(g => g.Key)
            .Select(g => new PeriodGroup
            {
                PeriodStart = new DateTimeOffset(g.Key, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(g.Key.AddMonths(1).AddDays(-1), TimeSpan.Zero),
                CommitCount = g.Count(),
                CommitIds = [.. g.Select(c => c.Sha)]
            })
            .ToList();

        logger.LogInformation("Grouped {CommitCount} commits into {GroupCount} months",
            commits.Count, groups.Count);

        return groups;
    }

    /// <summary>
    /// Group commits by calendar day.
    /// </summary>
    public List<PeriodGroup> GroupCommitsByDay(IReadOnlyList<Commit> commits)
    {
        var groups = commits
            .GroupBy(c => c.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new PeriodGroup
            {
                PeriodStart = new DateTimeOffset(g.Key, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(g.Key, TimeSpan.Zero),
                CommitCount = g.Count(),
                CommitIds = [.. g.Select(c => c.Sha)]
            })
            .ToList();

        logger.LogInformation("Grouped {CommitCount} commits into {GroupCount} days",
            commits.Count, groups.Count);

        return groups;
    }

    /// <summary>
    /// Group commits by ISO week (Monday-based).
    /// </summary>
    public List<PeriodGroup> GroupCommitsByWeek(IReadOnlyList<Commit> commits)
    {
        var groups = commits
            .GroupBy(c => GetWeekStart(c.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new PeriodGroup
            {
                PeriodStart = new DateTimeOffset(g.Key, TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(g.Key.AddDays(6), TimeSpan.Zero),
                CommitCount = g.Count(),
                CommitIds = [.. g.Select(c => c.Sha)]
            })
            .ToList();

        logger.LogInformation("Grouped {CommitCount} commits into {GroupCount} weeks",
            commits.Count, groups.Count);

        return groups;
    }

    /// <summary>
    /// Group commits by calendar year.
    /// </summary>
    public List<PeriodGroup> GroupCommitsByYear(IReadOnlyList<Commit> commits)
    {
        var groups = commits
            .GroupBy(c => c.Timestamp.Year)
            .OrderBy(g => g.Key)
            .Select(g => new PeriodGroup
            {
                PeriodStart = new DateTimeOffset(new DateTime(g.Key, 1, 1), TimeSpan.Zero),
                PeriodEnd = new DateTimeOffset(new DateTime(g.Key, 12, 31), TimeSpan.Zero),
                CommitCount = g.Count(),
                CommitIds = [.. g.Select(c => c.Sha)]
            })
            .ToList();

        logger.LogInformation("Grouped {CommitCount} commits into {GroupCount} years",
            commits.Count, groups.Count);

        return groups;
    }

    private static DateTime GetWeekStart(DateTimeOffset timestamp)
    {
        var date = timestamp.Date;
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>
    /// Calculate node positions for period groups.
    /// Distributes groups evenly across canvas width.
    /// Radius scales with commit count (4-20px).
    /// </summary>
    public static List<LayoutNode> CalculateNodePositions(
        List<PeriodGroup> groups,
        int canvasWidth,
        CalendarGranularity granularity = CalendarGranularity.Month)
    {
        if (groups.Count == 0) return [];

        const int marginX = 50;
        var usableWidth = canvasWidth - (2 * marginX);
        var spacing = groups.Count > 1 ? usableWidth / (groups.Count - 1) : 0;

        var maxCommits = groups.Max(g => g.CommitCount);

        var nodes = groups.Select((group, index) => new LayoutNode
        {
            CommitId = FormatPeriodId(group, granularity),
            X = marginX + (index * spacing),
            Y = CenterY, // Single horizontal row (Phase 4a)
            Radius = CalculateNodeRadius(group.CommitCount, maxCommits),
            BranchName = "all", // All branches aggregated
            Timestamp = group.PeriodStart,
            Message = FormatPeriodLabel(group, granularity),
            Author = string.Empty, // Not applicable for period groups
            IsSignificant = group.CommitCount > maxCommits * 0.7 // Top 30% activity
        }).ToList();

        return nodes;
    }

    /// <summary>
    /// Calculate node radius based on commit count.
    /// Scales logarithmically from MinRadius (4px) to MaxRadius (20px).
    /// </summary>
    public static double CalculateNodeRadius(int commitCount, int maxCommits)
    {
        if (commitCount <= 0) return MinRadius;
        if (maxCommits <= 0) return MinRadius;

        // Logarithmic scaling for better visual distribution
        var normalized = Math.Log(commitCount + 1) / Math.Log(maxCommits + 1);
        var radius = MinRadius + (normalized * (MaxRadius - MinRadius));

        return Math.Round(radius, 1);
    }

    private static string FormatPeriodId(PeriodGroup group, CalendarGranularity granularity) =>
        granularity switch
        {
            CalendarGranularity.Day => $"period-{group.PeriodStart:yyyy-MM-dd}",
            CalendarGranularity.Week => $"period-{group.PeriodStart:yyyy-MM-dd}",
            CalendarGranularity.Year => $"period-{group.PeriodStart:yyyy}",
            _ => $"period-{group.PeriodStart:yyyy-MM}"
        };

    private static string FormatPeriodLabel(PeriodGroup group, CalendarGranularity granularity)
    {
        var count = group.CommitCount;
        var suffix = count != 1 ? "s" : "";
        return granularity switch
        {
            CalendarGranularity.Day => $"{group.PeriodStart:yyyy-MM-dd}: {count} commit{suffix}",
            CalendarGranularity.Week => $"Week of {group.PeriodStart:MMM d, yyyy}: {count} commit{suffix}",
            CalendarGranularity.Year => $"{group.PeriodStart.Year}: {count} commit{suffix}",
            _ => $"{group.PeriodStart:MMMM yyyy}: {count} commit{suffix}"
        };
    }
}

/// <summary>
/// Represents a time period group of commits.
/// Public model for calendar layout calculations (exposed for testing).
/// </summary>
public record PeriodGroup
{
    public required DateTimeOffset PeriodStart { get; init; }
    public required DateTimeOffset PeriodEnd { get; init; }
    public required int CommitCount { get; init; }
    public required List<string> CommitIds { get; init; }
}
