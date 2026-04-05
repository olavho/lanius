using Lanius.Business.Layout.Models;
using Lanius.Business.Services;
using Microsoft.Extensions.Logging;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Logical layout engine that positions all commits on branch lines.
/// Shows complete commit history with branch splits and merges.
/// </summary>
public class LogicalLayoutEngine(
    ICommitAnalyzer commitAnalyzer,
    IBranchAnalyzer branchAnalyzer,
    IRepositoryService repositoryService,
    ILoggerFactory loggerFactory) : ILayoutEngine
{
    private readonly ILogger<LogicalLayoutEngine> _logger = loggerFactory.CreateLogger<LogicalLayoutEngine>();
    public async Task<LayoutResult> CalculateLayoutAsync(
        string repositoryId,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(options);

        progress?.Report(new LayoutProgress(0, "Loading branches", 0, 0));

        // Load branches
        var branches = await LoadBranchesAsync(repositoryId, options.BranchFilter, cancellationToken);
        if (branches.Count == 0)
        {
            return CreateEmptyResult(options.Mode);
        }

        progress?.Report(new LayoutProgress(10, $"Found {branches.Count} branches", branches.Count, branches.Count));

        // Load all commits for each branch
        var branchCommits = await LoadAllCommitsAsync(repositoryId, branches, progress, cancellationToken);
        var allCommits = branchCommits.Values.SelectMany(c => c).DistinctBy(c => c.Sha).ToList();

        if (allCommits.Count == 0)
        {
            return CreateEmptyResult(options.Mode);
        }

        progress?.Report(new LayoutProgress(50, $"Loaded {allCommits.Count} commits", allCommits.Count, allCommits.Count));

        // Assign Y lanes to branches
        var branchLanes = AssignBranchLanes(branches, options);

        // Calculate node positions
        var nodes = CalculateNodePositions(allCommits, branchCommits, branchLanes, options);

        progress?.Report(new LayoutProgress(80, $"Calculating edges for {nodes.Count} nodes", nodes.Count, nodes.Count));

        // Calculate edges (branch lines)
        var edges = CalculateEdges(nodes, branchCommits);

        progress?.Report(new LayoutProgress(100, $"Layout complete: {allCommits.Count} commits, {branches.Count} branches", allCommits.Count, allCommits.Count));

        // Calculate final dimensions
        var (width, height) = CalculateDimensions(nodes, options);

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

    private async Task<List<Lanius.Business.Models.Branch>> LoadBranchesAsync(
        string repositoryId,
        string? branchFilter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchFilter))
        {
            return [.. (await branchAnalyzer.GetBranchesAsync(repositoryId, includeRemote: true, cancellationToken))];
        }

        var patterns = branchFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. (await branchAnalyzer.GetBranchesByPatternAsync(repositoryId, patterns, cancellationToken))];
    }

    private async Task<Dictionary<string, List<Lanius.Business.Models.Commit>>> LoadAllCommitsAsync(
        string repositoryId,
        List<Lanius.Business.Models.Branch> branches,
        IProgress<LayoutProgress>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading commits for {BranchCount} branches using branch hierarchy optimization", branches.Count);

        // Phase 1: Analyze branch hierarchy (10-20% of progress)
        progress?.Report(new LayoutProgress(10, "Analyzing branch hierarchy...", 0, branches.Count));

        var hierarchyAnalyzer = new BranchHierarchyAnalyzer(
            repositoryService,
            loggerFactory.CreateLogger<BranchHierarchyAnalyzer>());

        var hierarchyInfo = await hierarchyAnalyzer.AnalyzeBranchHierarchyAsync(
            repositoryId,
            branches,
            new Progress<LayoutProgress>(p =>
            {
                // Scale hierarchy progress from 10% to 20%
                var scaledPercentage = 10 + (int)(p.Percentage * 0.1);
                progress?.Report(new LayoutProgress(
                    scaledPercentage,
                    $"Analyzing hierarchy: {p.Operation}",
                    p.ProcessedItems,
                    p.TotalItems));
            }),
            cancellationToken);

        _logger.LogInformation("Branch hierarchy analysis complete. Loading commits per branch...");

        // Phase 2: Load commits per branch (20-60% of progress)
        var result = new Dictionary<string, List<Lanius.Business.Models.Commit>>();
        int processedBranches = 0;
        int totalBranches = hierarchyInfo.Count;

        foreach (var branchInfo in hierarchyInfo)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Report progress BEFORE loading
            int percentage = 20 + (int)((processedBranches / (double)totalBranches) * 40);
            progress?.Report(new LayoutProgress(
                percentage,
                $"Loading commits: {branchInfo.Name} ({processedBranches + 1}/{totalBranches})",
                processedBranches,
                totalBranches));

            _logger.LogDebug("Loading commits for branch {BranchName} (Tier={Tier}, MergeBase={MergeBase}, EstCommits={EstCommits})",
                branchInfo.Name,
                branchInfo.Tier,
                branchInfo.MergeBaseSha?[..8] ?? "none",
                branchInfo.CommitCount);

            // Use optimized loading: only commits since merge base
            var commits = await commitAnalyzer.GetCommitsSinceAsync(
                repositoryId,
                branchInfo.Name,
                branchInfo.MergeBaseSha, // null for main branch (loads all)
                cancellationToken);

            result[branchInfo.Name] = [.. commits];

            _logger.LogDebug("Loaded {ActualCommits} commits for branch {BranchName}",
                commits.Count,
                branchInfo.Name);

            processedBranches++;
        }

        var totalCommits = result.Values.SelectMany(c => c).DistinctBy(c => c.Sha).Count();
        _logger.LogInformation("Commit loading complete. Total unique commits: {TotalCommits}", totalCommits);

        return result;
    }

    private static Dictionary<string, double> AssignBranchLanes(
        List<Lanius.Business.Models.Branch> branches,
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
        List<Lanius.Business.Models.Commit> allCommits,
        Dictionary<string, List<Lanius.Business.Models.Commit>> branchCommits,
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
            // Find primary branch (prefer main/master, or first alphabetically)
            var commitBranches = commitToBranches.GetValueOrDefault(commit.Sha, []);
            var primaryBranch = commitBranches.OrderBy(b =>
                b == "main" ? 0 : b == "master" ? 1 : 2)
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
        Dictionary<string, List<Lanius.Business.Models.Commit>> branchCommits)
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
}
