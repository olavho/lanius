using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Perf.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Lanius.Business.Perf.Layout;

/// <summary>
/// Performance tests for LogicalLayoutEngine against a real repository.
/// Set LANIUS_PERF_REPO_PATH to a local git repo path to enable.
/// </summary>
[TestClass]
[TestCategory("Performance")]
public class LogicalLayoutEnginePerfTests : PerfTestBase
{

    [TestMethod]
    [Description("End-to-end logical layout calculation. Measures all internal phases via [PERF] logs.")]
    [DataRow(@"c:\local\private\apus")]
    [DataRow(@"c:\local\public\reactive")]
    [DataRow(@"c:\local\public\ai\semantic-kernel")]
    public async Task CalculateLogicalLayout_ReportsElapsedTime(string repoPath)
    {
        SetRepoPath(repoPath);

        if (!IsRepoConfigured)
            Assert.Inconclusive("Set LANIUS_PERF_REPO_PATH to a local git repository to run perf tests.");

        using var cts = CreateTimeoutCts(TimeSpan.FromMinutes(15));
        using var loggerFactory = CreateLoggerFactory();
        var storage = CreateStorageService(RepoPath!);
        var commitAnalyzer = new CommitAnalyzer(storage, loggerFactory.CreateLogger<CommitAnalyzer>());
        var branchAnalyzer = new BranchAnalyzer(storage);
        var hierarchyAnalyzer = new BranchHierarchyAnalyzer(storage, loggerFactory.CreateLogger<BranchHierarchyAnalyzer>());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var engine = new LogicalLayoutEngine(commitAnalyzer, branchAnalyzer, hierarchyAnalyzer, storage, cache, loggerFactory.CreateLogger<LogicalLayoutEngine>());

        var options = new LayoutOptions { Mode = LayoutMode.Logical, BranchFilter = BranchFilter };
        Console.WriteLine($"Starting logical layout from: {RepoPath}");
        Console.WriteLine($"Branch filter: {BranchFilter ?? "(none — all branches)"}");
        if (BranchFilter == null)
            Console.WriteLine("  Tip: set LANIUS_PERF_BRANCH_FILTER=main,project/* to limit scope");

        var phases = new Dictionary<string, long>();
        var sw = MeasureStart();

        var result = await engine.CalculateLayoutAsync(PerfRepoId, options, ConsoleProgress(), cts.Token);
        RecordPhase(phases, "CalculateLayoutAsync_total", sw);

        Console.WriteLine($"Commits: {result.TotalCommits}, Branches: {result.TotalBranches}, Nodes: {result.Nodes.Count}, Edges: {result.Edges.Count}");
        TestContext.WriteLine($"Commits: {result.TotalCommits}, Branches: {result.TotalBranches}");
        TestContext.WriteLine($"Nodes: {result.Nodes.Count}, Edges: {result.Edges.Count}");
        await WriteResultAsync(nameof(CalculateLogicalLayout_ReportsElapsedTime), phases);

        Assert.IsGreaterThan(0, result.TotalCommits, "Expected commits in layout result.");
    }

    [TestMethod]
    [Description("Logical layout with branch filter applied. Useful to isolate main/project branch loading.")]
    [DataRow(@"c:\local\private\apus")]
    [DataRow(@"c:\local\public\reactive")]
    [DataRow(@"c:\local\public\ai\semantic-kernel")]
    public async Task CalculateLogicalLayout_WithBranchFilter_ReportsElapsedTime(string repoPath)
    {
        SetRepoPath(repoPath);
        if (!IsRepoConfigured)
            Assert.Inconclusive("Set LANIUS_PERF_REPO_PATH to a local git repository to run perf tests.");

        using var cts = CreateTimeoutCts(TimeSpan.FromMinutes(5));
        using var loggerFactory = CreateLoggerFactory();
        var storage = CreateStorageService(RepoPath!);
        var commitAnalyzer = new CommitAnalyzer(storage, loggerFactory.CreateLogger<CommitAnalyzer>());
        var branchAnalyzer = new BranchAnalyzer(storage);
        var hierarchyAnalyzer = new BranchHierarchyAnalyzer(storage, loggerFactory.CreateLogger<BranchHierarchyAnalyzer>());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var engine = new LogicalLayoutEngine(commitAnalyzer, branchAnalyzer, hierarchyAnalyzer, storage, cache, loggerFactory.CreateLogger<LogicalLayoutEngine>());

        // Filter to main + project/* to measure the typical dashboard scenario
        var options = new LayoutOptions { Mode = LayoutMode.Logical, BranchFilter = "main, master, project/*" };
        Console.WriteLine($"Starting filtered logical layout from: {RepoPath}");
        Console.WriteLine($"Branch filter: {options.BranchFilter}");

        var phases = new Dictionary<string, long>();
        var sw = MeasureStart();

        var result = await engine.CalculateLayoutAsync(PerfRepoId, options, ConsoleProgress(), cts.Token);
        RecordPhase(phases, "CalculateLayoutAsync_filtered_total", sw);

        Console.WriteLine($"Commits: {result.TotalCommits}, Branches: {result.TotalBranches}, Nodes: {result.Nodes.Count}");
        TestContext.WriteLine($"Commits: {result.TotalCommits}, Branches: {result.TotalBranches}");
        await WriteResultAsync(nameof(CalculateLogicalLayout_WithBranchFilter_ReportsElapsedTime), phases);

        Assert.IsGreaterThanOrEqualTo(0, result.TotalCommits);
    }
}
