using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Services;
using Lanius.Business.Perf.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Lanius.Business.Perf.Analysis;

/// <summary>
/// Performance tests for BranchAnalyzer against a real repository.
/// Set LANIUS_PERF_REPO_PATH to a local git repo path to enable.
/// </summary>
[TestClass]
[TestCategory("Performance")]
[Ignore("Performance testing - run manually when needed")]
public class BranchAnalyzerPerfTests : PerfTestBase
{
    [TestMethod]
    [Description("Measures time to enumerate all branches from a real repository.")]
    [DataRow(@"c:\local\private\apus")]
    [DataRow(@"c:\local\public\reactive")]
    [DataRow(@"c:\local\public\ai\semantic-kernel")]
    public async Task GetBranches_ReportsElapsedTime(string repoPath)
    {
        SetRepoPath(repoPath);

        if (!IsRepoConfigured)
            Assert.Inconclusive("Set LANIUS_PERF_REPO_PATH to a local git repository to run perf tests.");

        using var cts = CreateTimeoutCts(TimeSpan.FromMinutes(1));
        var storage = CreateStorageService(RepoPath!);
        var analyzer = new BranchAnalyzer(storage);
        var phases = new Dictionary<string, long>();
        var sw = MeasureStart();

        Console.WriteLine($"Loading branches from: {RepoPath}");
        var branches = await analyzer.GetBranchesAsync(PerfRepoId, includeRemote: true, cts.Token);
        RecordPhase(phases, "GetBranchesAsync", sw);

        Console.WriteLine($"Branches: {branches.Count}");
        TestContext.WriteLine($"Branches: {branches.Count}");
        await WriteResultAsync(nameof(GetBranches_ReportsElapsedTime), phases);

        Assert.IsNotEmpty(branches, "Expected at least one branch.");
    }

    [TestMethod]
    [Description("Measures branch hierarchy analysis (FindMergeBase) for all branches.")]
    [DataRow(@"c:\local\private\apus")]
    [DataRow(@"c:\local\public\reactive")]
    [DataRow(@"c:\local\public\ai\semantic-kernel")]
    public async Task AnalyzeBranchHierarchy_ReportsElapsedTime(string repoPath)
    {
        SetRepoPath(repoPath);

        if (!IsRepoConfigured)
            Assert.Inconclusive("Set LANIUS_PERF_REPO_PATH to a local git repository to run perf tests.");

        using var cts = CreateTimeoutCts(TimeSpan.FromMinutes(5));
        using var loggerFactory = CreateLoggerFactory();
        var storage = CreateStorageService(RepoPath!);
        var branchAnalyzer = new BranchAnalyzer(storage);
        var analyzer = new BranchHierarchyAnalyzer(storage, loggerFactory.CreateLogger<BranchHierarchyAnalyzer>());
        var phases = new Dictionary<string, long>();

        var allBranches = (await branchAnalyzer.GetBranchesAsync(PerfRepoId, includeRemote: true, cts.Token))
            .Where(b => b.IsRemote && b.Name.StartsWith("origin/"))
            .ToList();

        // Tier-ordered sample: main/master first, then project/*, release/*, others up to MaxBranches
        var branches = allBranches
            .OrderBy(b => b.Name is "origin/main" or "origin/master" ? 0 :
                          b.Name.StartsWith("origin/project/") ? 1 :
                          b.Name.StartsWith("origin/release/") ? 2 : 3)
            .ThenBy(b => b.Name)
            .Take(MaxBranches)
            .ToList();

        Console.WriteLine($"Analyzing {branches.Count} of {allBranches.Count} origin/* branches");
        Console.WriteLine($"(LANIUS_PERF_MAX_BRANCHES={MaxBranches} — set env var to change)");
        TestContext.WriteLine($"Total origin/* branches: {allBranches.Count}, analyzing: {branches.Count}");

        var sw = MeasureStart();
        var hierarchy = await analyzer.AnalyzeBranchHierarchyAsync(PerfRepoId, branches, ConsoleProgress(), cts.Token);
        RecordPhase(phases, "AnalyzeBranchHierarchyAsync", sw);

        Console.WriteLine($"Hierarchy results: {hierarchy.Count}");
        TestContext.WriteLine($"Hierarchy results: {hierarchy.Count}");
        await WriteResultAsync(nameof(AnalyzeBranchHierarchy_ReportsElapsedTime), phases);

        Assert.HasCount(branches.Count, hierarchy);
    }
}
