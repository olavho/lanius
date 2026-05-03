using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Microsoft.Extensions.Logging;
using DomainBranch = Lanius.Business.Analysis.Models.Branch;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Constellation layout engine: aesthetic-first topology view.
/// Positions commits on smooth branch ribbons with subtle deterministic jitter,
/// preserving branch membership and merge/split connectivity without strict date accuracy.
/// </summary>
public class ConstellationLayoutEngine(
    ICommitAnalyzer commitAnalyzer,
    IBranchAnalyzer branchAnalyzer,
    ILogger<ConstellationLayoutEngine> logger) : ILayoutEngine
{
    private const double MinRadius = 3.0;
    private const double MaxRadius = 8.0;

    public async Task<LayoutResult> CalculateLayoutAsync(
        string repositoryId,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        progress?.Report(new LayoutProgress(0, "Loading branches", 0, 0));
        var branches = await LoadBranchesAsync(repositoryId, options.BranchFilter, cancellationToken);
        if (branches.Count == 0)
        {
            return CreateEmptyResult(options);
        }

        progress?.Report(new LayoutProgress(15, "Loading commits", 0, 0));
        var batchRequest = branches
            .Select(b => (b.Name, sinceCommitSha: (string?)null))
            .ToList();

        var batchProgress = new Progress<(int processed, int total, string currentBranch)>(p =>
        {
            if (p.total == 0) return;
            var pct = 15 + (int)((p.processed / (double)p.total) * 30);
            progress?.Report(new LayoutProgress(pct, $"Loading commits: {p.currentBranch}", p.processed, p.total));
        });

        var batchCommits = await commitAnalyzer.GetCommitsBatchAsync(
            repositoryId,
            batchRequest,
            batchProgress,
            cancellationToken);

        var allCommits = MergeDistinctCommits(batchCommits);

        if (allCommits.Count == 0)
        {
            return CreateEmptyResult(options);
        }

        var branchOrder = branches.Select((b, i) => new { b.Name, Index = i })
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

        progress?.Report(new LayoutProgress(45, "Placing constellation nodes", 0, allCommits.Count));
        var sorted = allCommits.OrderBy(c => c.Timestamp).ThenBy(c => c.Sha, StringComparer.Ordinal).ToList();

        var laneHeight = Math.Max(28.0, options.BranchSpacing * 0.8);
        var centerY = options.MarginY + ((branches.Count - 1) * laneHeight / 2.0);
        var usableWidth = Math.Max(200.0, options.CanvasWidth - (2 * options.MarginX));

        var nodes = new List<LayoutNode>(sorted.Count);
        var commitToNode = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);

        for (int i = 0; i < sorted.Count; i++)
        {
            var commit = sorted[i];
            var primaryBranch = SelectPrimaryBranch(commit, branchOrder);
            if (!branchOrder.TryGetValue(primaryBranch, out var branchIndex))
            {
                branchIndex = branchOrder.Count > 0 ? 0 : branchIndex;
                primaryBranch = branchOrder.Keys.FirstOrDefault() ?? primaryBranch;
            }

            var xRatio = sorted.Count > 1 ? i / (double)(sorted.Count - 1) : 0.5;
            var x = options.MarginX + (xRatio * usableWidth);

            var laneBase = centerY + ((branchIndex - (branches.Count - 1) / 2.0) * laneHeight);
            var wave = Math.Sin((i * 0.18) + (branchIndex * 0.7)) * (laneHeight * 0.2);
            var jitter = GetDeterministicJitter(commit.Sha) * (laneHeight * 0.15);
            var y = laneBase + wave + jitter;

            var radius = CalculateRadius(commit);

            var node = new LayoutNode
            {
                CommitId = commit.Sha,
                X = x,
                Y = y,
                Radius = radius,
                BranchName = primaryBranch,
                Timestamp = commit.Timestamp,
                Message = commit.ShortMessage,
                Author = commit.Author,
                AuthorEmail = commit.AuthorEmail,
                Committer = commit.Committer,
                CommitterEmail = commit.CommitterEmail,
                CommitterTimestamp = commit.CommitterTimestamp,
                ParentShas = commit.ParentShas,
                IsSignificant = commit.IsMerge || commit.ParentShas.Count == 0,
                GridRow = branchIndex,
                GridColumn = i,
                IsGhost = false,
                CommitCount = 0
            };

            nodes.Add(node);
            commitToNode[commit.Sha] = node;

            if (i % 250 == 0)
            {
                var pct = 45 + (int)((i / (double)Math.Max(1, sorted.Count)) * 30);
                progress?.Report(new LayoutProgress(pct, "Placing constellation nodes", i, sorted.Count));
            }
        }

        progress?.Report(new LayoutProgress(78, "Building flow edges", 0, sorted.Count));
        var edges = BuildEdges(sorted, commitToNode);

        var width = Math.Max(options.CanvasWidth, nodes.Count > 0 ? nodes.Max(n => n.X) + options.MarginX : options.CanvasWidth);
        var height = Math.Max(options.CanvasHeight, nodes.Count > 0 ? nodes.Max(n => n.Y) + options.MarginY : options.CanvasHeight);

        progress?.Report(new LayoutProgress(100, $"Layout complete: {sorted.Count} commits, {branches.Count} branches", sorted.Count, sorted.Count));

        logger.LogInformation(
            "Constellation layout complete: {CommitCount} commits, {BranchCount} branches, {NodeCount} nodes, {EdgeCount} edges",
            sorted.Count,
            branches.Count,
            nodes.Count,
            edges.Count);

        return new LayoutResult
        {
            Mode = LayoutMode.Constellation,
            Nodes = nodes,
            Edges = edges,
            Width = width,
            Height = height,
            MinTimestamp = sorted.Min(c => c.Timestamp),
            MaxTimestamp = sorted.Max(c => c.Timestamp),
            TotalCommits = sorted.Count,
            TotalBranches = branches.Count,
            RowCount = branches.Count,
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

        return branches
            .Where(b => b.IsRemote && b.Name.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<Commit> MergeDistinctCommits(
        IReadOnlyDictionary<string, IReadOnlyList<Commit>> commitsByBranch)
    {
        var bySha = new Dictionary<string, Commit>(StringComparer.Ordinal);

        foreach (var (branchName, commits) in commitsByBranch)
        {
            foreach (var commit in commits)
            {
                if (!bySha.TryGetValue(commit.Sha, out var existing))
                {
                    var branches = commit.Branches.Count > 0
                        ? commit.Branches.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                        : new List<string> { branchName };

                    bySha[commit.Sha] = new Commit
                    {
                        Sha = commit.Sha,
                        Author = commit.Author,
                        AuthorEmail = commit.AuthorEmail,
                        Timestamp = commit.Timestamp,
                        Committer = commit.Committer,
                        CommitterEmail = commit.CommitterEmail,
                        CommitterTimestamp = commit.CommitterTimestamp,
                        Message = commit.Message,
                        ParentShas = commit.ParentShas,
                        Stats = commit.Stats,
                        Branches = branches
                    };

                    continue;
                }

                var mergedBranches = existing.Branches
                    .Concat(commit.Branches.Count > 0 ? commit.Branches : [branchName])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                bySha[commit.Sha] = new Commit
                {
                    Sha = existing.Sha,
                    Author = existing.Author,
                    AuthorEmail = existing.AuthorEmail,
                    Timestamp = existing.Timestamp,
                    Committer = existing.Committer,
                    CommitterEmail = existing.CommitterEmail,
                    CommitterTimestamp = existing.CommitterTimestamp,
                    Message = existing.Message,
                    ParentShas = existing.ParentShas,
                    Stats = existing.Stats,
                    Branches = mergedBranches
                };
            }
        }

        return bySha.Values.ToList();
    }

    private static string SelectPrimaryBranch(Commit commit, Dictionary<string, int> branchOrder)
    {
        if (commit.Branches.Count == 0)
        {
            return "origin/main";
        }

        return commit.Branches
            .OrderBy(b => branchOrder.TryGetValue(b, out var index) ? index : int.MaxValue)
            .ThenBy(b => b, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static double GetDeterministicJitter(string sha)
    {
        unchecked
        {
            var hash = 17;
            for (int i = 0; i < sha.Length; i++)
            {
                hash = (hash * 31) + sha[i];
            }

            var normalized = (hash & 0x7fffffff) / (double)int.MaxValue;
            return (normalized * 2.0) - 1.0;
        }
    }

    private static double CalculateRadius(Commit commit)
    {
        var changes = commit.Stats?.TotalChanges ?? 0;
        if (changes <= 0)
        {
            return MinRadius;
        }

        var scaled = Math.Log10(changes + 1);
        return Math.Clamp(MinRadius + (scaled * 1.6), MinRadius, MaxRadius);
    }

    private static List<LayoutEdge> BuildEdges(
        IReadOnlyList<Commit> commits,
        IReadOnlyDictionary<string, LayoutNode> nodeMap)
    {
        var edges = new List<LayoutEdge>();

        foreach (var commit in commits)
        {
            if (!nodeMap.TryGetValue(commit.Sha, out var targetNode))
            {
                continue;
            }

            for (int i = 0; i < commit.ParentShas.Count; i++)
            {
                var parentSha = commit.ParentShas[i];
                if (!nodeMap.TryGetValue(parentSha, out var parentNode))
                {
                    continue;
                }

                var sameBranch = string.Equals(parentNode.BranchName, targetNode.BranchName, StringComparison.OrdinalIgnoreCase);
                var type = sameBranch ? EdgeType.Normal : (i == 0 ? EdgeType.Branch : EdgeType.Merge);
                var direction = targetNode.Y > parentNode.Y
                    ? EdgeDirection.Downward
                    : targetNode.Y < parentNode.Y
                        ? EdgeDirection.Upward
                        : EdgeDirection.Horizontal;

                edges.Add(new LayoutEdge
                {
                    FromCommitId = parentSha,
                    ToCommitId = commit.Sha,
                    Type = type,
                    X1 = parentNode.X,
                    Y1 = parentNode.Y,
                    X2 = targetNode.X,
                    Y2 = targetNode.Y,
                    BranchName = targetNode.BranchName,
                    IsVertical = Math.Abs(parentNode.X - targetNode.X) < 0.001,
                    Direction = direction,
                    IsLongSpan = false
                });
            }
        }

        return edges;
    }

    private static LayoutResult CreateEmptyResult(LayoutOptions options)
    {
        return new LayoutResult
        {
            Mode = LayoutMode.Constellation,
            Nodes = [],
            Edges = [],
            Width = options.CanvasWidth,
            Height = options.CanvasHeight,
            TotalCommits = 0,
            TotalBranches = 0,
            RowCount = 0,
            ColumnCount = 0
        };
    }
}
