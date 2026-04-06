using Lanius.Business.Analysis.Models;
using Lanius.Business.Storage.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using DomainCommit = Lanius.Business.Analysis.Models.Commit;
using GitCommit = LibGit2Sharp.Commit;

namespace Lanius.Business.Analysis.Services;

/// <summary>
/// Service for analyzing Git commits using LibGit2Sharp.
/// </summary>
public class CommitAnalyzer(
    IRepositoryStorageService repositoryStorageService,
    ILogger<CommitAnalyzer> logger) : ICommitAnalyzer
{
    public async Task<IReadOnlyList<DomainCommit>> GetCommitsAsync(
        string repositoryId,
        string? branchName = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var repo = OpenRepository(repositoryId);

            var commits = branchName != null
                ? GetCommitsForBranch(repo, branchName)
                : [.. repo.Commits];

            return commits.Select(c => MapCommit(c, repo)).ToList() as IReadOnlyList<DomainCommit>;
        }, cancellationToken);
    }

    public Task<DomainCommit?> GetCommitAsync(string repositoryId, string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);

        using var repo = OpenRepository(repositoryId);
        var commit = repo.Lookup<GitCommit>(sha);

        if (commit == null)
        {
            return Task.FromResult<DomainCommit?>(null);
        }

        // Map the commit while repo is still open
        var result = MapCommit(commit, repo);
        return Task.FromResult<DomainCommit?>(result);
    }

    public async Task<IReadOnlyList<DomainCommit>> GetCommitsChronologicallyAsync(
        string repositoryId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default,
        IProgress<(int processed, int total)>? progress = null)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            using var repo = OpenRepository(repositoryId);
            logger.LogInformation("[PERF] OpenRepository (chronological): {ElapsedMs}ms", sw.ElapsedMilliseconds);

            sw.Restart();
            var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };
            var commits = repo.Commits.QueryBy(filter)
                .Where(c =>
                {
                    var timestamp = c.Author.When;
                    return (!startDate.HasValue || timestamp >= startDate.Value) &&
                           (!endDate.HasValue || timestamp <= endDate.Value);
                })
                .ToList();
            logger.LogInformation("[PERF] Enumerate (chronological): {ElapsedMs}ms ({CommitCount} commits)", sw.ElapsedMilliseconds, commits.Count);

            sw.Restart();
            IReadOnlyList<DomainCommit> result;
            if (progress == null)
            {
                result = commits.Select(MapCommitFast).ToList();
            }
            else
            {
                var total = commits.Count;
                var reportInterval = Math.Max(1, total / 100);
                var mapped = new List<DomainCommit>(total);
                for (int i = 0; i < total; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    mapped.Add(MapCommitFast(commits[i]));
                    if (i % reportInterval == 0 || i == total - 1)
                        progress.Report((i + 1, total));
                }
                result = mapped;
            }
            logger.LogInformation("[PERF] Map (chronological): {ElapsedMs}ms ({CommitCount} commits)", sw.ElapsedMilliseconds, commits.Count);
            return result;
        }, cancellationToken);
    }

    public Task<DiffStats?> GetCommitStatsAsync(string repositoryId, string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);

        using var repo = OpenRepository(repositoryId);
        var commit = repo.Lookup<GitCommit>(sha);

        if (commit == null)
        {
            return Task.FromResult<DiffStats?>(null);
        }

        // Calculate stats while repo is still open
        var stats = CalculateDiffStats(repo, commit);
        return Task.FromResult<DiffStats?>(stats);
    }

    private Repository OpenRepository(string repositoryId)
    {
        if (!repositoryStorageService.RepositoryExists(repositoryId))
            throw new InvalidOperationException($"Repository not found: {repositoryId}");
        return new Repository(repositoryStorageService.GetRepositoryPath(repositoryId));
    }

    private static DomainCommit MapCommit(GitCommit gitCommit, Repository repo)
    {
        var stats = CalculateDiffStats(repo, gitCommit);
        var branches = GetBranchesForCommit(repo, gitCommit);

        return new DomainCommit
        {
            Sha = gitCommit.Sha,
            Author = gitCommit.Author.Name,
            AuthorEmail = gitCommit.Author.Email,
            Timestamp = gitCommit.Author.When,
            Message = gitCommit.Message,
            ParentShas = [.. gitCommit.Parents.Select(p => p.Sha)],
            Stats = stats,
            Branches = branches
        };
    }

    /// <summary>
    /// Fast commit mapping for chronological (all-branches) walks.
    /// Skips diff stats and branch lookup — branches are not known in this context.
    /// </summary>
    private static DomainCommit MapCommitFast(GitCommit gitCommit) => MapCommitFast(gitCommit, string.Empty);

    /// <summary>
    /// Fast commit mapping that skips expensive O(n²) GetBranchesForCommit lookup.
    /// Use when loading commits for a specific branch (we already know which branch it belongs to).
    /// Skips diff stats calculation for performance (not needed for layout visualization).
    /// </summary>
    private static DomainCommit MapCommitFast(GitCommit gitCommit, string branchName)
    {
        // Skip diff stats entirely for layout - saves ~20ms per commit
        // Diff stats are only needed for commit detail views, not visualization
        var stats = new DiffStats
        {
            LinesAdded = 0,
            LinesRemoved = 0,
            FilesChanged = 0
        };

        return new DomainCommit
        {
            Sha = gitCommit.Sha,
            Author = gitCommit.Author.Name,
            AuthorEmail = gitCommit.Author.Email,
            Timestamp = gitCommit.Author.When,
            Message = gitCommit.Message,
            ParentShas = [.. gitCommit.Parents.Select(p => p.Sha)],
            Stats = stats,
            Branches = [branchName] // We already know the branch - skip expensive lookup
        };
    }

    private static DiffStats CalculateDiffStats(Repository repo, GitCommit commit)
    {
        if (!commit.Parents.Any())
        {
            // Initial commit - compare against empty tree
            var tree = commit.Tree;
            var patch = repo.Diff.Compare<Patch>(null, tree);
            return new DiffStats
            {
                LinesAdded = patch.LinesAdded,
                LinesRemoved = patch.LinesDeleted,
                FilesChanged = patch.Count()
            };
        }

        // Compare with first parent
        var parent = commit.Parents.First();
        var diffPatch = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree);

        return new DiffStats
        {
            LinesAdded = diffPatch.LinesAdded,
            LinesRemoved = diffPatch.LinesDeleted,
            FilesChanged = diffPatch.Count()
        };
    }

    private static List<GitCommit> GetCommitsForBranch(Repository repo, string branchName)
    {
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch not found: {branchName}");

        return [.. branch.Commits];
    }

    public async Task<IReadOnlyList<DomainCommit>> GetCommitsSinceAsync(
        string repositoryId,
        string branchName,
        string? sinceCommitSha = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            using var repo = OpenRepository(repositoryId);
            logger.LogInformation("[PERF] OpenRepository ({BranchName}): {ElapsedMs}ms", branchName, sw.ElapsedMilliseconds);
            return GetCommitsSinceInternal(repo, branchName, sinceCommitSha);
        }, cancellationToken);
    }

    public Task<Dictionary<string, IReadOnlyList<DomainCommit>>> GetCommitsBatchAsync(
        string repositoryId,
        IReadOnlyList<(string branchName, string? sinceCommitSha)> branches,
        IProgress<(int processed, int total, string currentBranch)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            using var repo = OpenRepository(repositoryId);
            logger.LogInformation("[PERF] OpenRepository (batch {BranchCount} branches): {ElapsedMs}ms", branches.Count, sw.ElapsedMilliseconds);
            return GetCommitsBatchFromRepo(repo, branches, progress, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Loads commits for multiple branches using an already-open repository.
    /// Called directly by LogicalLayoutEngine (B1) to share a single repository instance.
    /// </summary>
    internal Dictionary<string, IReadOnlyList<DomainCommit>> GetCommitsBatchFromRepo(
        Repository repo,
        IReadOnlyList<(string branchName, string? sinceCommitSha)> branches,
        IProgress<(int processed, int total, string currentBranch)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        var result = new Dictionary<string, IReadOnlyList<DomainCommit>>(branches.Count);

        for (int i = 0; i < branches.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (branchName, sinceCommitSha) = branches[i];
            progress?.Report((i, branches.Count, branchName));
            result[branchName] = GetCommitsSinceInternal(repo, branchName, sinceCommitSha);
        }

        progress?.Report((branches.Count, branches.Count, string.Empty));
        logger.LogInformation("[PERF] GetCommitsBatch total: {ElapsedMs}ms ({BranchCount} branches)", totalSw.ElapsedMilliseconds, branches.Count);
        return result;
    }

    private IReadOnlyList<DomainCommit> GetCommitsSinceInternal(
        Repository repo,
        string branchName,
        string? sinceCommitSha = null)
    {
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch not found: {branchName}");

        var sw = Stopwatch.StartNew();

        // B2: use native CommitFilter to bound the walk at the merge base
        var filter = new CommitFilter
        {
            IncludeReachableFrom = branch.Tip,
            SortBy = CommitSortStrategies.Topological
        };
        if (sinceCommitSha != null)
        {
            var mergeBase = repo.Lookup<GitCommit>(sinceCommitSha);
            if (mergeBase != null)
                filter.ExcludeReachableFrom = mergeBase;
            else
                logger.LogWarning("Merge base {Sha} not found, loading full branch history for {BranchName}",
                    sinceCommitSha[..8], branchName);
        }

        var commits = repo.Commits.QueryBy(filter).ToList();
        logger.LogInformation("[PERF] Enumerate {BranchName}: {ElapsedMs}ms ({CommitCount} commits, mergeBase={MergeBase})",
            branchName, sw.ElapsedMilliseconds, commits.Count, sinceCommitSha?[..8] ?? "none");

        sw.Restart();
        var result = commits.Select(c => MapCommitFast(c, branchName)).ToList() as IReadOnlyList<DomainCommit>;
        logger.LogInformation("[PERF] Map {BranchName}: {ElapsedMs}ms ({CommitCount} commits)",
            branchName, sw.ElapsedMilliseconds, result.Count);

        return result;
    }

    private static List<string> GetBranchesForCommit(Repository repo, GitCommit commit)
    {
        var branches = repo.Branches
            .Where(b => b.Commits.Any(c => c.Sha == commit.Sha))
            .Select(b => b.FriendlyName)
            .ToList();

        return branches;
    }
}
