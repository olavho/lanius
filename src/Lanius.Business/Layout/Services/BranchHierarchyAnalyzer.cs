using Lanius.Business.Layout.Models;
using Lanius.Business.Storage.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using DomainBranch = Lanius.Business.Analysis.Models.Branch;

namespace Lanius.Business.Layout.Services;

/// <summary>
/// Analyzes branch hierarchy to determine parent relationships and merge bases.
/// </summary>
public class BranchHierarchyAnalyzer(
    IRepositoryStorageService repositoryStorageService,
    ILogger<BranchHierarchyAnalyzer> logger) : IBranchHierarchyAnalyzer
{
    /// <summary>
    /// Analyze branch hierarchy for efficient commit loading.
    /// </summary>
    public async Task<List<BranchHierarchyInfo>> AnalyzeBranchHierarchyAsync(
        string repositoryId,
        List<DomainBranch> branches,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Analyzing branch hierarchy for {BranchCount} branches", branches.Count);

        // For 0 or 1 branches, no hierarchy analysis needed - skip repository access
        if (branches.Count <= 1)
        {
            logger.LogDebug("Skipping hierarchy analysis for {BranchCount} branch(es)", branches.Count);
            return [.. branches.Select(b => new BranchHierarchyInfo
            {
                Name = b.Name,
                Tier = GetBranchTier(b.Name),
                ParentBranchName = null,
                MergeBaseSha = null,
                CommitCount = 0
            })];
        }

        var sw = Stopwatch.StartNew();
        var result = await Task.Run(() =>
        {
            try
            {
                using var repo = OpenRepository(repositoryId);
                return AnalyzeBranchHierarchyWithRepository(repo, branches, progress);
            }
            catch (Exception ex) when (ex is LibGit2Sharp.RepositoryNotFoundException or InvalidOperationException)
            {
                // Repository not accessible (e.g., in tests) - return simple hierarchy without merge bases
                logger.LogWarning("Unable to access repository for hierarchy analysis, using simple tier-based hierarchy: {Error}", ex.Message);
                return [.. branches.Select(b => new BranchHierarchyInfo
                {
                    Name = b.Name,
                    Tier = GetBranchTier(b.Name),
                    ParentBranchName = null,
                    MergeBaseSha = null,
                    CommitCount = 0
                })];
            }
        }, cancellationToken);
        logger.LogInformation("[PERF] AnalyzeBranchHierarchy: {ElapsedMs}ms ({BranchCount} branches)",
            sw.ElapsedMilliseconds, result.Count);
        return result;
    }

    internal List<BranchHierarchyInfo> AnalyzeBranchHierarchyWithRepository(
        Repository repo,
        List<DomainBranch> branches,
        IProgress<LayoutProgress>? progress)
    {
        var result = new List<BranchHierarchyInfo>();
        int processed = 0;

        // Group branches by tier
        var branchesByTier = branches
            .Select(b => new { Branch = b, Tier = GetBranchTier(b.Name) })
            .OrderBy(x => x.Tier)
            .ToList();

        logger.LogDebug("Branch tier distribution: Main={Main}, Project={Project}, Release={Release}, Feature={Feature}, Other={Other}",
            branchesByTier.Count(x => x.Tier == BranchTier.Main),
            branchesByTier.Count(x => x.Tier == BranchTier.Project),
            branchesByTier.Count(x => x.Tier == BranchTier.Release),
            branchesByTier.Count(x => x.Tier == BranchTier.Feature),
            branchesByTier.Count(x => x.Tier == BranchTier.Other));

        // Process in tier order (Main → Project → Release → Feature → Other)
        foreach (var item in branchesByTier)
        {
            var percentage = (int)((processed / (double)branches.Count) * 100);
            progress?.Report(new LayoutProgress(
                percentage,
                $"Analyzing branch hierarchy: {item.Branch.Name} ({processed + 1}/{branches.Count})",
                processed,
                branches.Count));

            var libgit2Branch = repo.Branches[item.Branch.Name];
            if (libgit2Branch == null)
            {
                logger.LogWarning("Branch not found in repository: {BranchName}", item.Branch.Name);
                processed++;
                continue;
            }

            // Find parent branch and merge base
            var branchSw = Stopwatch.StartNew();
            var (parentName, mergeBaseSha, splitTimestamp, commitCount) = FindBranchPoint(repo, libgit2Branch, item.Tier, result);
            logger.LogDebug("[PERF] FindBranchPoint {BranchName}: {ElapsedMs}ms (parent={Parent}, commits={Commits})",
                item.Branch.Name, branchSw.ElapsedMilliseconds, parentName ?? "none", commitCount);

            var hierarchyInfo = new BranchHierarchyInfo
            {
                Name = item.Branch.Name,
                Tier = item.Tier,
                ParentBranchName = parentName,
                MergeBaseSha = mergeBaseSha,
                SplitTimestamp = splitTimestamp,
                CommitCount = commitCount
            };

            result.Add(hierarchyInfo);

            logger.LogDebug("Branch {BranchName}: Tier={Tier}, Parent={Parent}, MergeBase={MergeBase}, Commits={Commits}",
                hierarchyInfo.Name,
                hierarchyInfo.Tier,
                hierarchyInfo.ParentBranchName ?? "none",
                hierarchyInfo.MergeBaseSha?[..8] ?? "none",
                hierarchyInfo.CommitCount);

            processed++;
        }

        progress?.Report(new LayoutProgress(100, "Branch hierarchy analysis complete", branches.Count, branches.Count));
        logger.LogInformation("Branch hierarchy analysis complete. Total branches: {Count}", result.Count);
        return result;
    }

    /// <summary>
    /// Find branch point (parent branch and merge base commit).
    /// </summary>
    private (string? ParentName, string? MergeBaseSha, DateTimeOffset? SplitTimestamp, int CommitCount) FindBranchPoint(
        Repository repo,
        Branch branch,
        BranchTier tier,
        List<BranchHierarchyInfo> analyzedBranches)
    {
        // Main branch has no parent
        if (tier == BranchTier.Main)
        {
            var commitCount = branch.Commits.Count();
            logger.LogDebug("Main branch {BranchName}: {CommitCount} commits (no parent)",
                branch.FriendlyName, commitCount);
            return (null, null, null, commitCount);
        }

        // Find potential parent branches
        var candidates = analyzedBranches
            .Where(b => b.Tier < tier)
            .OrderBy(b => b.Tier)
            .ToList();

        if (candidates.Count == 0)
        {
            logger.LogWarning("No parent candidates found for {BranchName} (Tier={Tier}). Using full history.",
                branch.FriendlyName, tier);
            var commitCount = branch.Commits.Count();
            return (null, null, null, commitCount);
        }

        // Try each candidate parent (Main first, then Project, then Release)
        foreach (var candidate in candidates)
        {
            var parentBranch = repo.Branches[candidate.Name];
            if (parentBranch == null) continue;

            try
            {
                var mergeBaseSw = Stopwatch.StartNew();
                var mergeBase = repo.ObjectDatabase.FindMergeBase(branch.Tip, parentBranch.Tip);
                logger.LogDebug("[PERF] FindMergeBase {Branch} vs {Parent}: {ElapsedMs}ms",
                    branch.FriendlyName, candidate.Name, mergeBaseSw.ElapsedMilliseconds);
                if (mergeBase != null)
                {
                    // B2: use CommitFilter to count commits since merge base
                    int commitCount = repo.Commits.QueryBy(new CommitFilter
                    {
                        IncludeReachableFrom = branch.Tip,
                        ExcludeReachableFrom = mergeBase
                    }).Count();

                    logger.LogDebug("Found merge base for {BranchName} from {ParentName}: {MergeBaseSha}, {CommitCount} commits since branch point",
                        branch.FriendlyName,
                        candidate.Name,
                        mergeBase.Sha[..8],
                        commitCount);

                    return (candidate.Name, mergeBase.Sha, mergeBase.Committer.When, commitCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to find merge base between {BranchName} and {ParentName}",
                    branch.FriendlyName, candidate.Name);
            }
        }

        // No merge base found with any parent - use full history
        logger.LogWarning("No merge base found for {BranchName}. Using full history.", branch.FriendlyName);
        var fullCommitCount = branch.Commits.Count();
        return (null, null, null, fullCommitCount);
    }

    /// <summary>
    /// Determine branch tier based on naming convention.
    /// </summary>
    private static BranchTier GetBranchTier(string branchName)
    {
        // Normalize branch name (remove leading origin/ prefix only)
        var normalized = branchName.StartsWith("origin/", StringComparison.OrdinalIgnoreCase)
            ? branchName["origin/".Length..]
            : branchName;

        return normalized switch
        {
            "main" or "master" => BranchTier.Main,
            _ when normalized.StartsWith("project/") => BranchTier.Project,
            _ when normalized.StartsWith("release/") => BranchTier.Release,
            _ when normalized.StartsWith("feature/") ||
                   normalized.StartsWith("bugfix/") ||
                   normalized.StartsWith("hotfix/") => BranchTier.Feature,
            _ => BranchTier.Other
        };
    }

    private Repository OpenRepository(string repositoryId)
    {
        if (!repositoryStorageService.RepositoryExists(repositoryId))
            throw new InvalidOperationException($"Repository not found: {repositoryId}");
        return new Repository(repositoryStorageService.GetRepositoryPath(repositoryId));
    }
}
