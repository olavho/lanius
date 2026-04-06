using Lanius.Business.Layout.Services;
using Lanius.Business.Storage.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Text.Json;

[assembly: DoNotParallelize]

namespace Lanius.Business.Perf.Infrastructure;

/// <summary>
/// Base class for performance tests. Tests are skipped unless LANIUS_PERF_REPO_PATH
/// points to a local git repository.
/// </summary>
public abstract class PerfTestBase
{
    // Set this env var to a local git repository path to enable perf tests.
    // Example: $env:LANIUS_PERF_REPO_PATH = "C:\repos\some-repo"

    private static string? _repoPath;
    protected static string? RepoPath => _repoPath ?? Environment.GetEnvironmentVariable("LANIUS_PERF_REPO_PATH");

    protected const string PerfRepoId = "perf-repo";

    protected static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    protected static bool IsRepoConfigured =>
        !string.IsNullOrWhiteSpace(RepoPath) && Directory.Exists(RepoPath);

    // Max branches to analyze in hierarchy tests. Set LANIUS_PERF_MAX_BRANCHES to override.
    protected static readonly int MaxBranches =
        int.TryParse(Environment.GetEnvironmentVariable("LANIUS_PERF_MAX_BRANCHES"), out var max) ? max : 50;

    // Optional branch filter for layout tests. Set LANIUS_PERF_BRANCH_FILTER to use.
    // Example: $env:LANIUS_PERF_BRANCH_FILTER = "main,project/*"
    protected static readonly string? BranchFilter =
        Environment.GetEnvironmentVariable("LANIUS_PERF_BRANCH_FILTER");

    protected void SetRepoPath(string repoPath)
    {
        _repoPath = repoPath;
    }

    public TestContext TestContext { get; set; } = null!;

    protected static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(b => b
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

    protected static IRepositoryStorageService CreateStorageService(string repoPath)
    {
        var mock = new Mock<IRepositoryStorageService>();
        mock.Setup(s => s.RepositoryExists(PerfRepoId)).Returns(true);
        mock.Setup(s => s.GetRepositoryPath(PerfRepoId)).Returns(repoPath);
        return mock.Object;
    }

    /// <summary>
    /// Creates a CancellationTokenSource linked to TestContext.CancellationToken with a timeout.
    /// Use inside a <c>using</c> statement.
    /// </summary>
    protected CancellationTokenSource CreateTimeoutCts(TimeSpan? timeout = null)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(timeout ?? TimeSpan.FromMinutes(10));
        return cts;
    }

    /// <summary>
    /// Returns a progress handler that writes each step to stdout immediately (not buffered).
    /// </summary>
    protected static IProgress<LayoutProgress> ConsoleProgress() =>
        new Progress<LayoutProgress>(p =>
            Console.WriteLine($"  [{p.Percentage,3}%] {p.Operation}"));

    protected static Stopwatch MeasureStart() => Stopwatch.StartNew();

    protected void RecordPhase(Dictionary<string, long> phases, string name, Stopwatch sw)
    {
        phases[name] = sw.ElapsedMilliseconds;
        var msg = $"[PERF] {name}: {sw.ElapsedMilliseconds}ms";
        Console.WriteLine(msg);
        TestContext.WriteLine(msg);
        sw.Restart();
    }

    protected async Task WriteResultAsync(string testName, Dictionary<string, long> phaseMs)
    {
        var result = new PerfResult(
            MachineName: Environment.MachineName,
            DotNetVersion: Environment.Version.ToString(),
            TestName: testName,
            RepositoryPath: RepoPath ?? "unknown",
            Timestamp: DateTimeOffset.UtcNow,
            PhaseMs: phaseMs);

        var outputDir = ResolveOutputDir();
        if (outputDir == null)
        {
            TestContext.WriteLine("[PERF] Could not resolve docs/perf/ directory — results not saved.");
            return;
        }

        Directory.CreateDirectory(outputDir);
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var fileName = $"{date}-{Environment.MachineName.ToLowerInvariant()}.json";
        var filePath = Path.Combine(outputDir, fileName);

        var allResults = new List<PerfResult>();
        if (File.Exists(filePath))
        {
            try
            {
                var existing = await File.ReadAllTextAsync(filePath, cancellationToken: TestContext.CancellationToken);
                allResults = JsonSerializer.Deserialize<List<PerfResult>>(existing) ?? [];
            }
            catch { /* ignore corrupt file */ }
        }

        allResults.Add(result);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(allResults, Options), cancellationToken: TestContext.CancellationToken);
        TestContext.WriteLine($"[PERF] Results written to: {filePath}");
    }

    private static string? ResolveOutputDir()
    {
        // Walk up from the assembly directory looking for a docs/ sibling (solution root).
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "docs")))
                return Path.Combine(dir, "docs", "perf");
            var parent = Path.GetDirectoryName(dir);
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }
}
