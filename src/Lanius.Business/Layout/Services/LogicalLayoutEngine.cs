using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Storage.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Caching.Memory;
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
    IMemoryCache commitCache,
    ILogger<LogicalLayoutEngine> logger) : ILayoutEngine
{
    private readonly IMemoryCache _commitCache = commitCache;
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
        var (branchCommits, hierarchyInfo) = await LoadAllCommitsAsync(repositoryId, branches, progress, cancellationToken);
        var allCommits = branchCommits.Values.SelectMany(c => c).DistinctBy(c => c.Sha).ToList();
        _logger.LogInformation("[PERF] LoadCommits: {ElapsedMs}ms ({CommitCount} unique commits)", sw.ElapsedMilliseconds, allCommits.Count);

        if (allCommits.Count == 0)
        {
            return CreateEmptyResult(options.Mode);
        }

        progress?.Report(new LayoutProgress(60, $"Loaded {allCommits.Count} commits", allCommits.Count, allCommits.Count));

        // Assign branch rows (0-based lane indices)
        var branchRows = AssignBranchLanes(branches, hierarchyInfo, options);

        // Calculate node positions
        sw.Restart();
        var nodes = CalculateNodePositions(allCommits, branchCommits, branchRows, options);
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
            TotalBranches = branches.Count,
            RowCount = branchRows.Count,
            ColumnCount = nodes.Count > 0 ? nodes.Max(n => n.GridColumn) + 1 : 0
        };
    }

    private async Task<List<DomainBranch>> LoadBranchesAsync(
        string repositoryId,
        string? branchFilter,
        CancellationToken cancellationToken)
    {
        List<DomainBranch> branches;
        if (string.IsNullOrWhiteSpace(branchFilter))
        {
            branches = (await branchAnalyzer.GetBranchesAsync(repositoryId, includeRemote: true, cancellationToken)).ToList();
        }
        else
        {
            var patterns = branchFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            branches = (await branchAnalyzer.GetBranchesByPatternAsync(repositoryId, patterns, cancellationToken)).ToList();
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

    private async Task<(Dictionary<string, List<DomainCommit>>, List<BranchHierarchyInfo>)> LoadAllCommitsAsync(
        string repositoryId,
        List<DomainBranch> branches,
        IProgress<LayoutProgress>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading commits for {BranchCount} branches using branch hierarchy optimization", branches.Count);
        progress?.Report(new LayoutProgress(10, "Analyzing branch hierarchy...", 0, branches.Count));

        var branchTipShas = branches.ToDictionary(b => b.Name, b => b.TipSha);
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
                        return (new Dictionary<string, List<DomainCommit>>(), hierarchyInfo);
                    }

                    _logger.LogInformation("Loading commits for {BranchCount} branches...", hierarchyInfo.Count);

                    var hierarchyDict = hierarchyInfo.ToDictionary(b => b.Name);

                    // Split branches into cache hits and misses
                    var cachedResult = new Dictionary<string, List<DomainCommit>>();
                    var uncachedBranches = new List<BranchHierarchyInfo>();
                    foreach (var info in hierarchyInfo)
                    {
                        var tipSha = branchTipShas.GetValueOrDefault(info.Name, string.Empty);
                        var cacheKey = BuildCommitCacheKey(repositoryId, info.Name, tipSha, info.MergeBaseSha);
                        if (_commitCache.TryGetValue(cacheKey, out List<DomainCommit>? cached) && cached != null)
                            cachedResult[info.Name] = cached;
                        else
                            uncachedBranches.Add(info);
                    }
                    _logger.LogInformation("[PERF] CommitCache: {HitCount} hits, {MissCount} misses", cachedResult.Count, uncachedBranches.Count);

                    if (uncachedBranches.Count == 0)
                    {
                        var totalCached = cachedResult.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
                        _logger.LogInformation("[PERF] GetCommitsBatch: 0ms (all from cache, {CommitCount} unique commits)", totalCached);
                        return (cachedResult, hierarchyInfo);
                    }

                    var batchRequest = uncachedBranches.Select(b => (b.Name, b.MergeBaseSha)).ToList();
                    var batchProgress = CreateBatchProgress(hierarchyDict, progress);
                    var expiry = TimeSpan.FromMinutes(10);

                    sw.Restart();
                    var batchResult = concreteAnalyzer.GetCommitsBatchFromRepo(repo, batchRequest, batchProgress, cancellationToken);
                    foreach (var kvp in batchResult)
                    {
                        var commits = kvp.Value.ToList();
                        cachedResult[kvp.Key] = commits;
                        var tipSha = branchTipShas.GetValueOrDefault(kvp.Key, string.Empty);
                        var cacheKey = BuildCommitCacheKey(repositoryId, kvp.Key, tipSha, hierarchyDict[kvp.Key].MergeBaseSha);
                        _commitCache.Set(cacheKey, commits, expiry);
                    }

                    var totalCommits = cachedResult.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
                    _logger.LogInformation("[PERF] GetCommitsBatch: {ElapsedMs}ms ({BranchCount} branches loaded, {CommitCount} unique commits total)",
                        sw.ElapsedMilliseconds, uncachedBranches.Count, totalCommits);
                    return (cachedResult, hierarchyInfo);
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
                return ([], []);
            }

            _logger.LogInformation("Loading commits for {BranchCount} branches...", hierarchyInfo.Count);

            var hierarchyDict = hierarchyInfo.ToDictionary(b => b.Name);

            // Split branches into cache hits and misses
            var cachedResult = new Dictionary<string, List<DomainCommit>>();
            var uncachedBranches = new List<BranchHierarchyInfo>();
            foreach (var info in hierarchyInfo)
            {
                var tipSha = branchTipShas.GetValueOrDefault(info.Name, string.Empty);
                var cacheKey = BuildCommitCacheKey(repositoryId, info.Name, tipSha, info.MergeBaseSha);
                if (_commitCache.TryGetValue(cacheKey, out List<DomainCommit>? cached) && cached != null)
                    cachedResult[info.Name] = cached;
                else
                    uncachedBranches.Add(info);
            }
            _logger.LogInformation("[PERF] CommitCache: {HitCount} hits, {MissCount} misses", cachedResult.Count, uncachedBranches.Count);

            if (uncachedBranches.Count == 0)
            {
                var totalCached = cachedResult.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
                _logger.LogInformation("[PERF] GetCommitsBatch: 0ms (all from cache, {CommitCount} unique commits)", totalCached);
                return (cachedResult, hierarchyInfo);
            }

            var batchRequest = uncachedBranches.Select(b => (b.Name, b.MergeBaseSha)).ToList();
            var batchProgress = CreateBatchProgress(hierarchyDict, progress);
            var expiry = TimeSpan.FromMinutes(10);

            var batchSw = Stopwatch.StartNew();
            var batchResult = await commitAnalyzer.GetCommitsBatchAsync(
                repositoryId, batchRequest, batchProgress, cancellationToken);
            foreach (var kvp in batchResult)
            {
                var commits = kvp.Value.ToList();
                cachedResult[kvp.Key] = commits;
                var tipSha = branchTipShas.GetValueOrDefault(kvp.Key, string.Empty);
                var cacheKey = BuildCommitCacheKey(repositoryId, kvp.Key, tipSha, hierarchyDict[kvp.Key].MergeBaseSha);
                _commitCache.Set(cacheKey, commits, expiry);
            }

            var totalCommits = cachedResult.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
            _logger.LogInformation("[PERF] GetCommitsBatch: {ElapsedMs}ms ({BranchCount} branches loaded, {CommitCount} unique commits total)",
                batchSw.ElapsedMilliseconds, uncachedBranches.Count, totalCommits);
            return (cachedResult, hierarchyInfo);
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

    private static Dictionary<string, int> AssignBranchLanes(
        List<DomainBranch> branches,
        IReadOnlyList<BranchHierarchyInfo> hierarchyInfo,
        LayoutOptions options)
    {
        var hierarchyMap = hierarchyInfo.ToDictionary(h => h.Name);

        static int GetTier(DomainBranch b, Dictionary<string, BranchHierarchyInfo> map) =>
            map.TryGetValue(b.Name, out var h) ? (int)h.Tier : (int)BranchTier.Other;

        static DateTimeOffset GetSplitTs(DomainBranch b, Dictionary<string, BranchHierarchyInfo> map) =>
            map.TryGetValue(b.Name, out var h) && h.SplitTimestamp.HasValue
                ? h.SplitTimestamp.Value
                : DateTimeOffset.MaxValue;

        var sorted = branches.ToList();
        sorted.Sort((a, b) =>
        {
            var tierCmp = GetTier(a, hierarchyMap).CompareTo(GetTier(b, hierarchyMap));
            if (tierCmp != 0) return tierCmp;

            var tsCmp = GetSplitTs(a, hierarchyMap).CompareTo(GetSplitTs(b, hierarchyMap));
            if (tsCmp != 0) return tsCmp;

            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        var rows = new Dictionary<string, int>();
        for (int i = 0; i < sorted.Count; i++)
            rows[sorted[i].Name] = i;
        return rows;
    }

    private static List<LayoutNode> CalculateNodePositions(
        List<DomainCommit> allCommits,
        Dictionary<string, List<DomainCommit>> branchCommits,
        Dictionary<string, int> branchRows,
        LayoutOptions options)
    {
        var nodes = new List<LayoutNode>();

        // Sort commits chronologically; ties broken by SHA for determinism
        var sortedCommits = allCommits.OrderBy(c => c.Timestamp).ThenBy(c => c.Sha).ToList();

        // Track the next available column per row to prevent two nodes on the
        // same branch lane from sharing a column (handles identical timestamps).
        var nextColumnPerRow = new Dictionary<int, int>();

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

        for (int globalIndex = 0; globalIndex < sortedCommits.Count; globalIndex++)
        {
            var commit = sortedCommits[globalIndex];

            // Find primary branch (prefer main/master)
            var commitBranches = commitToBranches.GetValueOrDefault(commit.Sha, []);
            var primaryBranch = commitBranches
                .OrderBy(b => NormalizeBranchName(b) == "main" ? 0 :
                               NormalizeBranchName(b) == "master" ? 1 : 2)
                .ThenBy(b => b)
                .FirstOrDefault() ?? "unknown";

            var gridRow = branchRows.GetValueOrDefault(primaryBranch, 0);

            // Assign the next available column for this row, but never go below
            // the global chronological index (preserves left-to-right time order).
            var minColumn = nextColumnPerRow.GetValueOrDefault(gridRow, 0);
            var gridColumn = Math.Max(globalIndex, minColumn);
            nextColumnPerRow[gridRow] = gridColumn + 1;

            var x = options.MarginX + gridColumn * options.ColumnWidth;
            var y = options.MarginY + gridRow * options.BranchSpacing;

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
                IsSignificant = isSignificant,
                GridRow = gridRow,
                GridColumn = gridColumn
            });
        }

        // Second pass: add a ghost (shadow-ref) node for each branch split.
        // The ghost sits at (parentNode.X, childBranchY) — the vertical split edge anchors here,
        // and the branch lane line starts here. Real commit positions are untouched.
        var nodeDict = nodes.ToDictionary(n => n.CommitId);
        var allCommitsDict = allCommits.ToDictionary(c => c.Sha);
        var ghostNodes = new List<LayoutNode>();

        foreach (var node in nodes)
        {
            if (!allCommitsDict.TryGetValue(node.CommitId, out var c2)) continue;
            if (c2.IsMerge || c2.ParentShas.Count != 1) continue;

            var parentSha = c2.ParentShas[0];
            if (!nodeDict.TryGetValue(parentSha, out var parentNode)) continue;
            if (parentNode.BranchName == node.BranchName) continue;  // same-branch parent
            if (parentNode.X >= node.X) continue;                    // parent not to the left

            ghostNodes.Add(new LayoutNode
            {
                CommitId = $"ghost:{node.CommitId}",
                X = parentNode.X,
                Y = node.Y,
                Radius = 0,
                BranchName = node.BranchName,
                Timestamp = parentNode.Timestamp,
                Message = string.Empty,
                Author = string.Empty,
                IsSignificant = false,
                IsGhost = true,
                GridRow = node.GridRow,
                GridColumn = parentNode.GridColumn
            });
        }

        nodes.AddRange(ghostNodes);
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

                            // Branch (split) edges anchor at the parent commit's X so the line is
                            // vertical from the parent branch down to the ghost node on the child branch.
                            // Merge edges anchor at the child (merge) commit's X.
                            var anchorX = edgeType == EdgeType.Branch ? parentNode.X : childNode.X;

                            edges.Add(new LayoutEdge
                            {
                                FromCommitId = parentSha,
                                ToCommitId = commit.Sha,
                                Type = edgeType,
                                X1 = anchorX,
                                Y1 = parentNode.Y,
                                X2 = anchorX,
                                Y2 = childNode.Y,
                                BranchName = childBranch,
                                IsVertical = true,
                                Direction = parentNode.Y < childNode.Y
                                    ? EdgeDirection.Downward
                                    : EdgeDirection.Upward
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
                            X1 = fromNode.X,
                            Y1 = fromNode.Y,
                            X2 = toNode.X,
                            Y2 = toNode.Y,
                            BranchName = branchName,
                            IsVertical = false,
                            Direction = EdgeDirection.Horizontal
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

    private static string BuildCommitCacheKey(string repositoryId, string branchName, string tipSha, string? mergeBaseSha)
        => $"{repositoryId}:commits:{branchName}:{tipSha}:{mergeBaseSha ?? "root"}";
}
