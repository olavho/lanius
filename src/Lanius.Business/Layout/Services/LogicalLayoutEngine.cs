using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Storage.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using DomainBranch = Lanius.Business.Analysis.Models.Branch;
using DomainCommit = Lanius.Business.Analysis.Models.Commit;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Logical layout engine that positions all commits on branch lines.
/// Shows complete commit history with branch splits and merges.
/// </summary>
public class LogicalLayoutEngine(
    ICommitAnalyzer commitAnalyzer,
    IBranchAnalyzer branchAnalyzer,
    IBranchHierarchyAnalyzer branchHierarchyAnalyzer,
    IRepositoryStorageService repositoryStorageService,
    ILogger<LogicalLayoutEngine> logger) : ILayoutEngine
{
    private readonly ILogger<LogicalLayoutEngine> _logger = logger;

    public async Task<LayoutResult> CalculateLayoutAsync(
        string repositoryId,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        progress?.Report(new LayoutProgress(0, "Loading branches", 0, 0));

        // Load branches
        var branches = await LoadBranchesAsync(repositoryId, options.BranchFilter, cancellationToken);
        _logger.LogInformation("[PERF] LoadBranches: {ElapsedMs}ms ({BranchCount} branches)", sw.ElapsedMilliseconds, branches.Count);
        if (branches.Count == 0)
        {
            return CreateEmptyResult(options.Mode);
        }

        progress?.Report(new LayoutProgress(10, $"Found {branches.Count} branches", branches.Count, branches.Count));

        // Load all commits for each branch
        sw.Restart();
        var branchCommits = await LoadAllCommitsAsync(repositoryId, branches, progress, cancellationToken);
        var allCommits = branchCommits.Values.SelectMany(c => c).DistinctBy(c => c.Sha).ToList();
        _logger.LogInformation("[PERF] LoadCommits: {ElapsedMs}ms ({CommitCount} unique commits)", sw.ElapsedMilliseconds, allCommits.Count);

        if (allCommits.Count == 0)
        {
            return CreateEmptyResult(options.Mode);
        }

        progress?.Report(new LayoutProgress(60, $"Loaded {allCommits.Count} commits", allCommits.Count, allCommits.Count));

        // Assign Y lanes to branches
        var branchLanes = AssignBranchLanes(branches, options);

        // Calculate node positions
        sw.Restart();
        var nodes = CalculateNodePositions(allCommits, branchCommits, branchLanes, options);
        _logger.LogInformation("[PERF] CalculateNodes: {ElapsedMs}ms ({NodeCount} nodes)", sw.ElapsedMilliseconds, nodes.Count);

        progress?.Report(new LayoutProgress(80, $"Calculating edges for {nodes.Count} nodes", nodes.Count, nodes.Count));

        // Calculate edges (branch lines)
        sw.Restart();
        var edges = CalculateEdges(nodes, branchCommits);
        _logger.LogInformation("[PERF] CalculateEdges: {ElapsedMs}ms ({EdgeCount} edges)", sw.ElapsedMilliseconds, edges.Count);

        progress?.Report(new LayoutProgress(100, $"Layout complete: {allCommits.Count} commits, {branches.Count} branches", allCommits.Count, allCommits.Count));

        // Calculate final dimensions
        var (width, height) = CalculateDimensions(nodes, options);

        _logger.LogInformation("[PERF] CalculateLayout total: {ElapsedMs}ms ({CommitCount} commits, {BranchCount} branches)",
            totalSw.ElapsedMilliseconds, allCommits.Count, branches.Count);

        return new LayoutResult
        {
            Mode = options.Mode,
            Nodes = nodes,
            Edges = edges,
            Width = width,
            Height = height,
            MinTimestamp = allCommits.Min(c => c.Timestamp),
            MaxTimestamp = allCommits.Max(c => c.Timestamp),
            TotalCommits = allCommits.Count,
            TotalBranches = branches.Count
        };
    }

    private async Task<List<DomainBranch>> LoadBranchesAsync(
        string repositoryId,
        string? branchFilter,
        CancellationToken cancellationToken)
    {
        // Always load all branches first so filtering can normalize names consistently
        // (e.g. pattern "main" should match remote "origin/main").
        var allBranches = (await branchAnalyzer.GetBranchesAsync(repositoryId, includeRemote: true, cancellationToken)).ToList();

        List<DomainBranch> branches;
        if (string.IsNullOrWhiteSpace(branchFilter))
        {
            branches = allBranches;
        }
        else
        {
            var patterns = branchFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            branches =
            [
                .. allBranches.Where(b =>
                    MatchesAnyPattern(NormalizeBranchName(b.Name), patterns) ||
                    MatchesAnyPattern(b.Name, patterns))
            ];
        }

        // Filter to origin/* branches only to avoid duplicate processing
        // (local tracking branches like 'main' are duplicates of 'origin/main')
        var remoteBranches = branches
            .Where(b => b.IsRemote && b.Name.StartsWith("origin/"))
            .ToList();

        _logger.LogInformation(
            "Loaded {TotalBranches} branches, filtered to {RemoteBranches} origin/* branches",
            branches.Count,
            remoteBranches.Count);

        return remoteBranches;
    }

    private async Task<Dictionary<string, List<DomainCommit>>> LoadAllCommitsAsync(
        string repositoryId,
        List<DomainBranch> branches,
        IProgress<LayoutProgress>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading commits for {BranchCount} branches using branch hierarchy optimization", branches.Count);
        progress?.Report(new LayoutProgress(10, "Analyzing branch hierarchy...", 0, branches.Count));

        var hierarchyProgress = new Progress<LayoutProgress>(p =>
        {
            var scaledPercentage = 10 + (int)(p.Percentage * 0.1);
            progress?.Report(new LayoutProgress(scaledPercentage, $"Analyzing hierarchy: {p.Operation}", p.ProcessedItems, p.TotalItems));
        });

        // B1: open the repository once and share it between hierarchy analysis and commit batch loading.
        // Falls back to separate opens when RepositoryExists returns false (e.g. in unit tests with mocked services).
        if (repositoryStorageService.RepositoryExists(repositoryId)
            && commitAnalyzer is CommitAnalyzer concreteAnalyzer
            && branchHierarchyAnalyzer is BranchHierarchyAnalyzer concreteHierarchyAnalyzer)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    using var repo = new Repository(repositoryStorageService.GetRepositoryPath(repositoryId));

                    var hierarchyInfo = concreteHierarchyAnalyzer.AnalyzeBranchHierarchyWithRepository(repo, branches, hierarchyProgress);
                    _logger.LogInformation("[PERF] BranchHierarchyAnalysis: {ElapsedMs}ms ({BranchCount} branches)",
                        sw.ElapsedMilliseconds, hierarchyInfo.Count);

                    if (hierarchyInfo.Count == 0)
                    {
                        _logger.LogInformation("No branches to load commits from");
                        return new Dictionary<string, List<DomainCommit>>();
                    }

                    _logger.LogInformation("Loading commits for {BranchCount} branches...", hierarchyInfo.Count);

                    var hierarchyDict = hierarchyInfo.ToDictionary(b => b.Name);
                    var batchRequest = hierarchyInfo.Select(b => (b.Name, b.MergeBaseSha)).ToList();
                    var batchProgress = CreateBatchProgress(hierarchyDict, progress);

                    sw.Restart();
                    var batchResult = concreteAnalyzer.GetCommitsBatchFromRepo(repo, batchRequest, batchProgress, cancellationToken);
                    var result = batchResult.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
                    var totalCommits = result.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
                    _logger.LogInformation("[PERF] GetCommitsBatch: {ElapsedMs}ms ({BranchCount} branches, {CommitCount} unique commits)",
                        sw.ElapsedMilliseconds, branches.Count, totalCommits);
                    return result;
                }, cancellationToken);
            }
            catch (RepositoryNotFoundException ex)
            {
                _logger.LogDebug("Repository not accessible for shared session ({Error}), falling back to individual opens", ex.Message);
            }
        }

        // Fallback: separate async calls — used in tests with mocked ICommitAnalyzer / IRepositoryStorageService
        {
            var hierarchySw = Stopwatch.StartNew();
            var hierarchyInfo = await branchHierarchyAnalyzer.AnalyzeBranchHierarchyAsync(
                repositoryId, branches, hierarchyProgress, cancellationToken);
            _logger.LogInformation("[PERF] BranchHierarchyAnalysis: {ElapsedMs}ms ({BranchCount} branches)",
                hierarchySw.ElapsedMilliseconds, hierarchyInfo.Count);

            if (hierarchyInfo.Count == 0)
            {
                _logger.LogInformation("No branches to load commits from");
                return [];
            }

            _logger.LogInformation("Loading commits for {BranchCount} branches...", hierarchyInfo.Count);

            var hierarchyDict = hierarchyInfo.ToDictionary(b => b.Name);
            var batchRequest = hierarchyInfo.Select(b => (b.Name, b.MergeBaseSha)).ToList();
            var batchProgress = CreateBatchProgress(hierarchyDict, progress);

            var batchSw = Stopwatch.StartNew();
            var batchResult = await commitAnalyzer.GetCommitsBatchAsync(
                repositoryId, batchRequest, batchProgress, cancellationToken);
            var result = batchResult.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
            var totalCommits = result.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
            _logger.LogInformation("[PERF] GetCommitsBatch: {ElapsedMs}ms ({BranchCount} branches, {CommitCount} unique commits)",
                batchSw.ElapsedMilliseconds, branches.Count, totalCommits);
            return result;
        }
    }

    private IProgress<(int processed, int total, string currentBranch)> CreateBatchProgress(
        Dictionary<string, BranchHierarchyInfo> hierarchyDict,
        IProgress<LayoutProgress>? progress)
    {
        return new Progress<(int processed, int total, string currentBranch)>(p =>
        {
            if (p.total == 0) return;
            int percentage = 20 + (int)((p.processed / (double)p.total) * 40);
            progress?.Report(new LayoutProgress(
                percentage,
                $"Loading commits: {p.currentBranch} ({p.processed + 1}/{p.total})",
                p.processed,
                p.total));

            if (!string.IsNullOrEmpty(p.currentBranch) && hierarchyDict.TryGetValue(p.currentBranch, out var info))
            {
                _logger.LogInformation(
                    "Loading commits for branch {BranchName} (Tier={Tier}, MergeBase={MergeBase}, EstCommits={EstCommits})",
                    info.Name, info.Tier, info.MergeBaseSha?[..8] ?? "none", info.CommitCount);
            }
        });
    }

    private static Dictionary<string, double> AssignBranchLanes(
        List<DomainBranch> branches,
        LayoutOptions options)
    {
        var lanes = new Dictionary<string, double>();
        double currentY = options.MarginY;

        foreach (var branch in branches.OrderBy(b => b.Name))
        {
            lanes[branch.Name] = currentY;
            currentY += options.BranchSpacing;
        }

        return lanes;
    }

    private static List<LayoutNode> CalculateNodePositions(
        List<DomainCommit> allCommits,
        Dictionary<string, List<DomainCommit>> branchCommits,
        Dictionary<string, double> branchLanes,
        LayoutOptions options)
    {
        var nodes = new List<LayoutNode>();

        // Sort commits chronologically
        var sortedCommits = allCommits.OrderBy(c => c.Timestamp).ToList();
        var minTime = sortedCommits.First().Timestamp;
        var maxTime = sortedCommits.Last().Timestamp;
        var timeRange = (maxTime - minTime).TotalSeconds;

        if (timeRange == 0)
        {
            timeRange = 1; // Avoid division by zero
        }

        // Available width for timeline
        var availableWidth = options.CanvasWidth - (2 * options.MarginX);

        // Create lookup: commit SHA -> branches it belongs to
        var commitToBranches = new Dictionary<string, List<string>>();
        foreach (var (branchName, commits) in branchCommits)
        {
            foreach (var commit in commits)
            {
                if (!commitToBranches.TryGetValue(commit.Sha, out List<string>? value))
                {
                    value = [];
                    commitToBranches[commit.Sha] = value;
                }

                value.Add(branchName);
            }
        }

        foreach (var commit in sortedCommits)
        {
            // Find primary branch (prefer main/master, including origin/main|origin/master)
            var commitBranches = commitToBranches.GetValueOrDefault(commit.Sha, []);
            var primaryBranch = commitBranches.OrderBy(b =>
                NormalizeBranchName(b) == "main" ? 0 :
                NormalizeBranchName(b) == "master" ? 1 : 2)
                .ThenBy(b => b)
                .FirstOrDefault() ?? "unknown";

            // Calculate X position based on timestamp
            var timeDelta = (commit.Timestamp - minTime).TotalSeconds;
            var x = options.MarginX + (timeDelta / timeRange * availableWidth);

            // Get Y position from branch lane
            var y = branchLanes.GetValueOrDefault(primaryBranch, options.MarginY);

            // Determine if significant (merge commits)
            var isSignificant = commit.IsMerge;

            nodes.Add(new LayoutNode
            {
                CommitId = commit.Sha,
                X = x,
                Y = y,
                Radius = isSignificant ? 6 : 4,
                BranchName = primaryBranch,
                Timestamp = commit.Timestamp,
                Message = commit.Message,
                Author = commit.Author,
                IsSignificant = isSignificant
            });
        }

        return nodes;
    }

    private static List<LayoutEdge> CalculateEdges(
        List<LayoutNode> nodes,
        Dictionary<string, List<DomainCommit>> branchCommits)
    {
        var edges = new List<LayoutEdge>();
        var nodeDict = nodes.ToDictionary(n => n.CommitId);

        // Build commit lookup for parent/child relationships
        var allCommits = branchCommits.Values.SelectMany(c => c).DistinctBy(c => c.Sha).ToDictionary(c => c.Sha);
        var commitToBranch = new Dictionary<string, string>();
        foreach (var (branchName, commits) in branchCommits)
        {
            foreach (var commit in commits)
            {
                // Map commit to its primary branch (first occurrence)
                commitToBranch.TryAdd(commit.Sha, branchName);
            }
        }

        // Track processed edges to avoid duplicates
        var processedEdges = new HashSet<(string, string)>();

        // First, add cross-branch edges for splits and merges
        foreach (var commit in allCommits.Values)
        {
            if (!nodeDict.TryGetValue(commit.Sha, out var childNode))
                continue;

            var childBranch = commitToBranch.GetValueOrDefault(commit.Sha);

            // Check each parent for cross-branch relationships
            foreach (var parentSha in commit.ParentShas)
            {
                if (!nodeDict.TryGetValue(parentSha, out var parentNode))
                    continue;

                var parentBranch = commitToBranch.GetValueOrDefault(parentSha);

                // If parent is on different branch, this is a branch split or merge
                if (parentBranch != childBranch && !string.IsNullOrEmpty(parentBranch) && !string.IsNullOrEmpty(childBranch))
                {
                    var edgeKey = (parentSha, commit.Sha);
                    if (processedEdges.Add(edgeKey))
                    {
                        // Determine edge type: if commit has multiple parents, it's a merge
                        var edgeType = commit.ParentShas.Count > 1 ? EdgeType.Merge : EdgeType.Branch;

                        edges.Add(new LayoutEdge
                        {
                            FromCommitId = parentSha,
                            ToCommitId = commit.Sha,
                            Type = edgeType,
                            Points =
                            [
                                [parentNode.X, parentNode.Y],
                                [childNode.X, childNode.Y]
                            ],
                            BranchName = childBranch // Edge belongs to the child branch
                        });
                    }
                }
            }
        }

        // Then create edges along each branch (excluding cross-branch edges already added)
        foreach (var (branchName, commits) in branchCommits)
        {
            var sortedCommits = commits.OrderBy(c => c.Timestamp).ToList();

            for (int i = 0; i < sortedCommits.Count - 1; i++)
            {
                var fromCommit = sortedCommits[i];
                var toCommit = sortedCommits[i + 1];

                // Only create edge if both commits belong to this branch
                if (commitToBranch.GetValueOrDefault(fromCommit.Sha) == branchName &&
                    commitToBranch.GetValueOrDefault(toCommit.Sha) == branchName &&
                    nodeDict.TryGetValue(fromCommit.Sha, out var fromNode) &&
                    nodeDict.TryGetValue(toCommit.Sha, out var toNode))
                {
                    var edgeKey = (fromCommit.Sha, toCommit.Sha);
                    if (processedEdges.Add(edgeKey))
                    {
                        edges.Add(new LayoutEdge
                        {
                            FromCommitId = fromCommit.Sha,
                            ToCommitId = toCommit.Sha,
                            Type = EdgeType.Normal,
                            Points =
                            [
                                [fromNode.X, fromNode.Y],
                                [toNode.X, toNode.Y]
                            ],
                            BranchName = branchName
                        });
                    }
                }
            }
        }

        return edges;
    }

    private static (double width, double height) CalculateDimensions(
        List<LayoutNode> nodes,
        LayoutOptions options)
    {
        if (nodes.Count == 0)
        {
            return (options.CanvasWidth, options.CanvasHeight);
        }

        var maxX = nodes.Max(n => n.X) + options.MarginX;
        var maxY = nodes.Max(n => n.Y) + options.MarginY;

        return (Math.Max(maxX, options.CanvasWidth), Math.Max(maxY, options.CanvasHeight));
    }

    private static LayoutResult CreateEmptyResult(LayoutMode mode)
    {
        return new LayoutResult
        {
            Mode = mode,
            Nodes = [],
            Edges = [],
            Width = 1200,
            Height = 600,
            TotalCommits = 0,
            TotalBranches = 0
        };
    }

    private static string NormalizeBranchName(string branchName)
    {
        const string remotePrefix = "origin/";
        return branchName.StartsWith(remotePrefix, StringComparison.OrdinalIgnoreCase)
            ? branchName[remotePrefix.Length..]
            : branchName;
    }

    private static bool MatchesAnyPattern(string branchName, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(branchName, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string branchName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        if (pattern == "*")
            return true;

        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                branchName,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return branchName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
