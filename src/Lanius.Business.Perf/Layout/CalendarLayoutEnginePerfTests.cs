using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Perf.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Lanius.Business.Perf.Layout;

/// <summary>
/// Performance tests for CalendarLayoutEngine against a real repository.
/// Set LANIUS_PERF_REPO_PATH to a local git repo path to enable.
/// </summary>
[TestClass]
[TestCategory("Performance")]
public class CalendarLayoutEnginePerfTests : PerfTestBase
{
    [TestMethod]
    [Description("End-to-end calendar layout. Exposes cost of full commit walk + mapping for all branches.")]
    [DataRow(@"c:\local\private\apus")]
    [DataRow(@"c:\local\public\reactive")]
    [DataRow(@"c:\local\public\ai\semantic-kernel")]
    public async Task CalculateCalendarLayout_ReportsElapsedTime(string repoPath)
    {
        SetRepoPath(repoPath);
        if (!IsRepoConfigured)
            Assert.Inconclusive("Set LANIUS_PERF_REPO_PATH to a local git repository to run perf tests.");

        using var cts = CreateTimeoutCts(TimeSpan.FromMinutes(10));
        using var loggerFactory = CreateLoggerFactory();
        var storage = CreateStorageService(RepoPath!);
        var commitAnalyzer = new CommitAnalyzer(storage, loggerFactory.CreateLogger<CommitAnalyzer>());
        var engine = new CalendarLayoutEngine(commitAnalyzer, loggerFactory.CreateLogger<CalendarLayoutEngine>());

        var options = new LayoutOptions { Mode = LayoutMode.Calendar };
        Console.WriteLine($"Starting calendar layout from: {RepoPath}");

        var phases = new Dictionary<string, long>();
        var sw = MeasureStart();

        var result = await engine.CalculateLayoutAsync(PerfRepoId, options, ConsoleProgress(), cts.Token);
        RecordPhase(phases, "CalculateLayoutAsync_total", sw);

        Console.WriteLine($"Commits: {result.TotalCommits}, Nodes: {result.Nodes.Count}");
        TestContext.WriteLine($"Commits: {result.TotalCommits}, Nodes: {result.Nodes.Count}");
        await WriteResultAsync(nameof(CalculateCalendarLayout_ReportsElapsedTime), phases);

        Assert.IsGreaterThanOrEqualTo(0, result.TotalCommits);
    }
}
