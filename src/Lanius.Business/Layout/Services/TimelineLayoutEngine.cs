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
/// Timeline layout engine: positions each commit at its real calendar date on a proportional x-axis.
/// Branch lanes occupy y-rows (same as Logical). Same-day commits on the same branch stack vertically.
/// Edges spanning more than 14 days are flagged as IsLongSpan for dashed rendering.
/// </summary>
public class TimelineLayoutEngine(
    ICommitAnalyzer commitAnalyzer,
    IBranchAnalyzer branchAnalyzer,
    IBranchHierarchyAnalyzer branchHierarchyAnalyzer,
    IRepositoryStorageService repositoryStorageService,
    IMemoryCache commitCache,
    ILogger<TimelineLayoutEngine> logger) : ILayoutEngine
{
    private const double LongSpanThresholdDays = 14.0;
    private const double SubRowSpacing = 12.0;
    private const double MinPixelsPerDay = 3.0;
    private const double MaxPixelsPerDay = 10.0;

    public async Task<LayoutResult> CalculateLayoutAsync(
        string repositoryId,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        var sw = Stopwatch.StartNew();
        progress?.Report(new LayoutProgress(0, "Loading branches", 0, 0));

        var branches = await LoadBranchesAsync(repositoryId, options.BranchFilter, cancellationToken);
        if (branches.Count == 0)
            return CreateEmptyResult();

        progress?.Report(new LayoutProgress(10, $"Found {branches.Count} branches", branches.Count, branches.Count));

        var (branchCommits, hierarchyInfo) = await LoadAllCommitsAsync(repositoryId, branches, progress, cancellationToken);
        var allCommits = branchCommits.Values.SelectMany(c => c).DistinctBy(c => c.Sha).ToList();
        if (allCommits.Count == 0)
            return CreateEmptyResult();

        progress?.Report(new LayoutProgress(60, $"Loaded {allCommits.Count} commits", allCommits.Count, allCommits.Count));

        var branchRows = AssignBranchLanes(branches, hierarchyInfo, branchCommits);

        // Compute time axis parameters
        var tMin = allCommits.Min(c => c.Timestamp);
        var tMax = allCommits.Max(c => c.Timestamp);
        var span = tMax - tMin;
        if (span.TotalSeconds < 1) span = TimeSpan.FromDays(1);

        var pixelsPerDay = Math.Max(MinPixelsPerDay, Math.Min(MaxPixelsPerDay, 800.0 / Math.Max(1, span.TotalDays)));
        var canvasWidth = Math.Max(options.CanvasWidth, options.MarginX * 2 + span.TotalDays * pixelsPerDay);
        var usableWidth = canvasWidth - options.MarginX * 2;

        var nodes = CalculateNodePositions(allCommits, branchCommits, branchRows, options, tMin, span.TotalSeconds, usableWidth);

        progress?.Report(new LayoutProgress(80, $"Calculating edges for {nodes.Count} nodes", nodes.Count, nodes.Count));

        var edges = CalculateEdges(nodes, branchCommits);

        progress?.Report(new LayoutProgress(100, $"Layout complete: {allCommits.Count} commits, {branches.Count} branches", allCommits.Count, allCommits.Count));

        var maxX = nodes.Count > 0 ? nodes.Max(n => n.X) + options.MarginX : canvasWidth;
        var maxY = nodes.Count > 0 ? nodes.Max(n => n.Y) + options.MarginY : options.CanvasHeight;

        logger.LogInformation("[PERF] TimelineLayout total: {ElapsedMs}ms ({CommitCount} commits, {BranchCount} branches)",
            sw.ElapsedMilliseconds, allCommits.Count, branches.Count);

        return new LayoutResult
        {
            Mode = LayoutMode.Timeline,
            Nodes = nodes,
            Edges = edges,
            Width = Math.Max(maxX, canvasWidth),
            Height = Math.Max(maxY, options.CanvasHeight),
            MinTimestamp = tMin,
            MaxTimestamp = tMax,
            TimelineOriginX = options.MarginX,
            TimelinePixelsPerSecond = span.TotalSeconds > 0 ? usableWidth / span.TotalSeconds : 0,
            TotalCommits = allCommits.Count,
            TotalBranches = branches.Count,
            RowCount = branchRows.Count,
            ColumnCount = nodes.Count > 0 ? nodes.Max(n => n.GridColumn) + 1 : 0
        };
    }

    // ── Branch loading ──────────────────────────────────────────────────────

    private async Task<List<DomainBranch>> LoadBranchesAsync(
        string repositoryId,
        string? branchFilter,
        CancellationToken cancellationToken)
    {
        List<DomainBranch> branches;
        if (string.IsNullOrWhiteSpace(branchFilter))
        {
            branches = [.. (await branchAnalyzer.GetBranchesAsync(repositoryId, includeRemote: true, cancellationToken))];
        }
        else
        {
            var patterns = branchFilter
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SelectMany(p => p.StartsWith("origin/", StringComparison.OrdinalIgnoreCase)
                    ? (IEnumerable<string>)[p]
                    : [p, "origin/" + p])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            branches = [.. (await branchAnalyzer.GetBranchesByPatternAsync(repositoryId, patterns, cancellationToken))];
        }

        var remote = branches.Where(b => b.IsRemote && b.Name.StartsWith("origin/")).ToList();
        logger.LogInformation("Timeline: loaded {Total} branches, {Remote} origin/* branches", branches.Count, remote.Count);
        return remote;
    }

    // ── Commit loading (shared path with LogicalLayoutEngine) ───────────────

    private async Task<(Dictionary<string, List<DomainCommit>>, List<BranchHierarchyInfo>)> LoadAllCommitsAsync(
        string repositoryId,
        List<DomainBranch> branches,
        IProgress<LayoutProgress>? progress,
        CancellationToken cancellationToken)
    {
        var branchTipShas = branches.ToDictionary(b => b.Name, b => b.TipSha);
        var hierarchyProgress = new Progress<LayoutProgress>(p =>
            progress?.Report(new LayoutProgress(10 + (int)(p.Percentage * 0.1), p.Operation, p.ProcessedItems, p.TotalItems)));

        // Shared-repo fast path (production)
        if (repositoryStorageService.RepositoryExists(repositoryId)
            && commitAnalyzer is CommitAnalyzer concreteAnalyzer
            && branchHierarchyAnalyzer is BranchHierarchyAnalyzer concreteHierarchyAnalyzer)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var repo = new Repository(repositoryStorageService.GetRepositoryPath(repositoryId));
                    var hierarchyInfo = concreteHierarchyAnalyzer.AnalyzeBranchHierarchyWithRepository(repo, branches, hierarchyProgress);
                    if (hierarchyInfo.Count == 0) return (new Dictionary<string, List<DomainCommit>>(), hierarchyInfo);

                    var hierarchyDict = hierarchyInfo.ToDictionary(b => b.Name);
                    var result = new Dictionary<string, List<DomainCommit>>();
                    var uncached = new List<BranchHierarchyInfo>();

                    foreach (var info in hierarchyInfo)
                    {
                        var key = BuildCacheKey(repositoryId, info.Name, branchTipShas.GetValueOrDefault(info.Name, ""), info.MergeBaseSha);
                        if (commitCache.TryGetValue(key, out List<DomainCommit>? cached) && cached != null)
                            result[info.Name] = cached;
                        else
                            uncached.Add(info);
                    }

                    if (uncached.Count > 0)
                    {
                        var batch = uncached.Select(b => (b.Name, b.MergeBaseSha)).ToList();
                        var batchResult = concreteAnalyzer.GetCommitsBatchFromRepo(repo, batch, null, cancellationToken);
                        foreach (var kvp in batchResult)
                        {
                            var commits = kvp.Value.ToList();
                            result[kvp.Key] = commits;
                            var key = BuildCacheKey(repositoryId, kvp.Key, branchTipShas.GetValueOrDefault(kvp.Key, ""), hierarchyDict[kvp.Key].MergeBaseSha);
                            commitCache.Set(key, commits, TimeSpan.FromMinutes(10));
                        }
                    }
                    return (result, hierarchyInfo);
                }, cancellationToken);
            }
            catch (RepositoryNotFoundException) { }
        }

        // Fallback (tests / mocked services)
        {
            var hierarchyInfo = await branchHierarchyAnalyzer.AnalyzeBranchHierarchyAsync(
                repositoryId, branches, hierarchyProgress, cancellationToken);
            if (hierarchyInfo.Count == 0) return ([], []);

            var hierarchyDict = hierarchyInfo.ToDictionary(b => b.Name);
            var result = new Dictionary<string, List<DomainCommit>>();
            var uncached = new List<BranchHierarchyInfo>();

            foreach (var info in hierarchyInfo)
            {
                var key = BuildCacheKey(repositoryId, info.Name, branchTipShas.GetValueOrDefault(info.Name, ""), info.MergeBaseSha);
                if (commitCache.TryGetValue(key, out List<DomainCommit>? cached) && cached != null)
                    result[info.Name] = cached;
                else
                    uncached.Add(info);
            }

            if (uncached.Count > 0)
            {
                var batchRequest = uncached.Select(b => (b.Name, b.MergeBaseSha)).ToList();
                var batchResult = await commitAnalyzer.GetCommitsBatchAsync(
                    repositoryId, batchRequest, null, cancellationToken);
                foreach (var kvp in batchResult)
                {
                    var commits = kvp.Value.ToList();
                    result[kvp.Key] = commits;
                    var key = BuildCacheKey(repositoryId, kvp.Key, branchTipShas.GetValueOrDefault(kvp.Key, ""), hierarchyDict[kvp.Key].MergeBaseSha);
                    commitCache.Set(key, commits, TimeSpan.FromMinutes(10));
                }
            }
            return (result, hierarchyInfo);
        }
    }

    // ── Lane assignment (identical to LogicalLayoutEngine) ──────────────────

    private static Dictionary<string, int> AssignBranchLanes(
        List<DomainBranch> branches,
        IReadOnlyList<BranchHierarchyInfo> hierarchyInfo,
        Dictionary<string, List<DomainCommit>> branchCommits)
    {
        var map = hierarchyInfo.ToDictionary(h => h.Name);

        static int GetTier(DomainBranch b, Dictionary<string, BranchHierarchyInfo> m) =>
            m.TryGetValue(b.Name, out var h) ? (int)h.Tier : (int)BranchTier.Other;
        static DateTimeOffset GetSplitTs(DomainBranch b, Dictionary<string, BranchHierarchyInfo> m) =>
            m.TryGetValue(b.Name, out var h) && h.SplitTimestamp.HasValue ? h.SplitTimestamp.Value : DateTimeOffset.MaxValue;
        static DateTimeOffset GetFirstTs(DomainBranch b, Dictionary<string, List<DomainCommit>> c) =>
            c.TryGetValue(b.Name, out var l) && l.Count > 0 ? l.Min(x => x.Timestamp) : DateTimeOffset.MaxValue;

        var sorted = branches.ToList();
        sorted.Sort((a, b) =>
        {
            var t = GetTier(a, map).CompareTo(GetTier(b, map));
            if (t != 0) return t;
            var s = GetSplitTs(a, map).CompareTo(GetSplitTs(b, map));
            if (s != 0) return s;
            var f = GetFirstTs(a, branchCommits).CompareTo(GetFirstTs(b, branchCommits));
            if (f != 0) return f;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        var rows = new Dictionary<string, int>();
        for (int i = 0; i < sorted.Count; i++)
            rows[sorted[i].Name] = i;
        return rows;
    }

    // ── Node positioning ────────────────────────────────────────────────────

    private static List<LayoutNode> CalculateNodePositions(
        List<DomainCommit> allCommits,
        Dictionary<string, List<DomainCommit>> branchCommits,
        Dictionary<string, int> branchRows,
        LayoutOptions options,
        DateTimeOffset tMin,
        double spanSeconds,
        double usableWidth)
    {
        // Commit → primary branch lookup
        var commitToBranches = new Dictionary<string, List<string>>();
        foreach (var (branch, commits) in branchCommits)
            foreach (var c in commits)
                commitToBranches.GetOrAdd(c.Sha).Add(branch);

        // (branch, day-key) → sub-row counter for clustering
        var subRowCounter = new Dictionary<(string, string), int>();

        var nodes = new List<LayoutNode>();
        var sorted = allCommits.OrderBy(c => c.Timestamp).ThenBy(c => c.Sha).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var commit = sorted[i];
            var branches = commitToBranches.GetValueOrDefault(commit.Sha, []);
            var primaryBranch = branches
                .OrderBy(b => NormalizeName(b) is "main" or "master" ? 0 : 1)
                .ThenBy(b => b)
                .FirstOrDefault() ?? "unknown";

            var gridRow = branchRows.GetValueOrDefault(primaryBranch, 0);
            var dayKey = commit.Timestamp.ToString("yyyy-MM-dd");
            var clusterKey = (primaryBranch, dayKey);
            var subRow = subRowCounter.GetValueOrDefault(clusterKey, 0);
            subRowCounter[clusterKey] = subRow + 1;

            var t = (commit.Timestamp - tMin).TotalSeconds;
            var x = options.MarginX + (spanSeconds > 0 ? t / spanSeconds * usableWidth : 0);
            var y = options.MarginY + gridRow * options.BranchSpacing + subRow * SubRowSpacing;

            nodes.Add(new LayoutNode
            {
                CommitId = commit.Sha,
                X = x,
                Y = y,
                Radius = commit.IsMerge ? 6 : 4,
                BranchName = primaryBranch,
                Timestamp = commit.Timestamp,
                Message = commit.Message,
                Author = commit.Author,
                AuthorEmail = commit.AuthorEmail,
                Committer = commit.Committer,
                CommitterEmail = commit.CommitterEmail,
                CommitterTimestamp = commit.CommitterTimestamp,
                ParentShas = commit.ParentShas,
                IsSignificant = commit.IsMerge,
                GridRow = gridRow,
                GridColumn = i
            });
        }

        // Ghost nodes for branch splits (same logic as Logical)
        var nodeDict = nodes.ToDictionary(n => n.CommitId);
        var allDict = allCommits.ToDictionary(c => c.Sha);
        var ghosts = new List<LayoutNode>();

        foreach (var node in nodes)
        {
            if (!allDict.TryGetValue(node.CommitId, out var c)) continue;
            if (c.IsMerge || c.ParentShas.Count != 1) continue;
            var parentSha = c.ParentShas[0];
            if (!nodeDict.TryGetValue(parentSha, out var parentNode)) continue;
            if (parentNode.BranchName == node.BranchName) continue;
            if (parentNode.X >= node.X) continue;

            ghosts.Add(new LayoutNode
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

        nodes.AddRange(ghosts);
        return nodes;
    }

    // ── Edge calculation ─────────────────────────────────────────────────────

    private static List<LayoutEdge> CalculateEdges(
        List<LayoutNode> nodes,
        Dictionary<string, List<DomainCommit>> branchCommits)
    {
        var edges = new List<LayoutEdge>();
        var nodeDict = nodes.ToDictionary(n => n.CommitId);
        var allCommits = branchCommits.Values.SelectMany(c => c).DistinctBy(c => c.Sha).ToDictionary(c => c.Sha);
        var commitToBranch = new Dictionary<string, string>();
        foreach (var (branch, commits) in branchCommits)
            foreach (var c in commits)
                commitToBranch.TryAdd(c.Sha, branch);

        var processed = new HashSet<(string, string)>();

        // Cross-branch edges (splits and merges)
        foreach (var commit in allCommits.Values)
        {
            if (!nodeDict.TryGetValue(commit.Sha, out var childNode)) continue;
            var childBranch = commitToBranch.GetValueOrDefault(commit.Sha);

            foreach (var parentSha in commit.ParentShas)
            {
                if (!nodeDict.TryGetValue(parentSha, out var parentNode)) continue;
                var parentBranch = commitToBranch.GetValueOrDefault(parentSha);
                if (parentBranch == childBranch || string.IsNullOrEmpty(parentBranch) || string.IsNullOrEmpty(childBranch))
                    continue;

                if (!processed.Add((parentSha, commit.Sha))) continue;
                var edgeType = commit.ParentShas.Count > 1 ? EdgeType.Merge : EdgeType.Branch;
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
                    Direction = parentNode.Y < childNode.Y ? EdgeDirection.Downward : EdgeDirection.Upward
                });
            }
        }

        // Within-branch edges (may span large time gaps)
        foreach (var (branchName, commits) in branchCommits)
        {
            var sorted = commits.OrderBy(c => c.Timestamp).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var from = sorted[i];
                var to = sorted[i + 1];
                if (commitToBranch.GetValueOrDefault(from.Sha) != branchName) continue;
                if (commitToBranch.GetValueOrDefault(to.Sha) != branchName) continue;
                if (!nodeDict.TryGetValue(from.Sha, out var fromNode)) continue;
                if (!nodeDict.TryGetValue(to.Sha, out var toNode)) continue;
                if (!processed.Add((from.Sha, to.Sha))) continue;

                var gapDays = (to.Timestamp - from.Timestamp).TotalDays;
                edges.Add(new LayoutEdge
                {
                    FromCommitId = from.Sha,
                    ToCommitId = to.Sha,
                    Type = EdgeType.Normal,
                    X1 = fromNode.X,
                    Y1 = fromNode.Y,
                    X2 = toNode.X,
                    Y2 = toNode.Y,
                    BranchName = branchName,
                    IsVertical = false,
                    Direction = EdgeDirection.Horizontal,
                    IsLongSpan = gapDays > LongSpanThresholdDays
                });
            }
        }

        return edges;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string NormalizeName(string name) =>
        name.StartsWith("origin/", StringComparison.OrdinalIgnoreCase) ? name[7..] : name;

    private static string BuildCacheKey(string repoId, string branch, string tipSha, string? mergeBaseSha) =>
        $"{repoId}:timeline:{branch}:{tipSha}:{mergeBaseSha ?? "root"}";

    private static LayoutResult CreateEmptyResult() => new()
    {
        Mode = LayoutMode.Timeline,
        Nodes = [],
        Edges = [],
        Width = 1200,
        Height = 600,
        TotalCommits = 0,
        TotalBranches = 0
    };
}

/// <summary>Extension to avoid TryGetValue boilerplate for List-valued dictionaries.</summary>
file static class DictExt
{
    internal static List<TVal> GetOrAdd<TKey, TVal>(this Dictionary<TKey, List<TVal>> dict, TKey key)
        where TKey : notnull
    {
        if (!dict.TryGetValue(key, out var list))
            dict[key] = list = [];
        return list;
    }
}
