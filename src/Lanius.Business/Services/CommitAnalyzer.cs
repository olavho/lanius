using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using DomainCommit = Lanius.Business.Models.Commit;
using GitCommit = LibGit2Sharp.Commit;

namespace Lanius.Business.Services;

/// <summary>
/// Service for analyzing Git commits using LibGit2Sharp.
/// </summary>
public class CommitAnalyzer(
    IRepositoryService repositoryService,
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
            using var repo = OpenRepository(repositoryId);

            var commits = repo.Commits
                .Where(c =>
                {
                    var timestamp = c.Author.When;
                    return (!startDate.HasValue || timestamp >= startDate.Value) &&
                           (!endDate.HasValue || timestamp <= endDate.Value);
                })
                .OrderBy(c => c.Author.When)
                .ToList();

            if (progress == null)
                return commits.Select(c => MapCommit(c, repo)).ToList() as IReadOnlyList<DomainCommit>;

            var total = commits.Count;
            var reportInterval = Math.Max(1, total / 100);
            var result = new List<DomainCommit>(total);
            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(MapCommit(commits[i], repo));
                if (i % reportInterval == 0 || i == total - 1)
                    progress.Report((i + 1, total));
            }
            return result as IReadOnlyList<DomainCommit>;
        }, cancellationToken);
    }

    public Task<Models.DiffStats?> GetCommitStatsAsync(string repositoryId, string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);

        using var repo = OpenRepository(repositoryId);
        var commit = repo.Lookup<GitCommit>(sha);

        if (commit == null)
        {
            return Task.FromResult<Models.DiffStats?>(null);
        }

        // Calculate stats while repo is still open
        var stats = CalculateDiffStats(repo, commit);
        return Task.FromResult<Models.DiffStats?>(stats);
    }

    private Repository OpenRepository(string repositoryId)
    {
        if (!repositoryService.RepositoryExists(repositoryId))
        {
            throw new InvalidOperationException($"Repository not found: {repositoryId}");
        }

        var info = repositoryService.GetRepositoryInfoAsync(repositoryId).Result;
        return new Repository(info!.LocalPath);
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
    /// Fast commit mapping that skips expensive O(n²) GetBranchesForCommit lookup.
    /// Use when loading commits for a specific branch (we already know which branch it belongs to).
    /// Skips diff stats calculation for performance (not needed for layout visualization).
    /// </summary>
    private static DomainCommit MapCommitFast(GitCommit gitCommit, string branchName)
    {
        // Skip diff stats entirely for layout - saves ~20ms per commit
        // Diff stats are only needed for commit detail views, not visualization
        var stats = new Models.DiffStats
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

    private static Models.DiffStats CalculateDiffStats(Repository repo, GitCommit commit)
    {
        if (!commit.Parents.Any())
        {
            // Initial commit - compare against empty tree
            var tree = commit.Tree;
            var patch = repo.Diff.Compare<Patch>(null, tree);
            return new Models.DiffStats
            {
                LinesAdded = patch.LinesAdded,
                LinesRemoved = patch.LinesDeleted,
                FilesChanged = patch.Count()
            };
        }

        // Compare with first parent
        var parent = commit.Parents.First();
        var diffPatch = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree);

        return new Models.DiffStats
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
            var repoOpenStart = DateTimeOffset.UtcNow;
            using var repo = OpenRepository(repositoryId);
            var repoOpenElapsed = (DateTimeOffset.UtcNow - repoOpenStart).TotalMilliseconds;

            logger.LogInformation("Opened repository for {BranchName} in {OpenTimeMs:F0}ms",
                branchName, repoOpenElapsed);

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
            var repoOpenStart = DateTimeOffset.UtcNow;
            using var repo = OpenRepository(repositoryId);
            var repoOpenElapsed = (DateTimeOffset.UtcNow - repoOpenStart).TotalMilliseconds;

            logger.LogInformation("Opened repository once for {BranchCount} branches in {OpenTimeMs:F0}ms",
                branches.Count, repoOpenElapsed);

            var result = new Dictionary<string, IReadOnlyList<DomainCommit>>(branches.Count);

            for (int i = 0; i < branches.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (branchName, sinceCommitSha) = branches[i];
                progress?.Report((i, branches.Count, branchName));
                result[branchName] = GetCommitsSinceInternal(repo, branchName, sinceCommitSha);
            }

            progress?.Report((branches.Count, branches.Count, string.Empty));
            return result;
        }, cancellationToken);
    }

    private IReadOnlyList<DomainCommit> GetCommitsSinceInternal(
        Repository repo,
        string branchName,
        string? sinceCommitSha = null)
    {
        var branch = repo.Branches[branchName]
            ?? throw new InvalidOperationException($"Branch not found: {branchName}");

        IEnumerable<GitCommit> commits;

        if (sinceCommitSha == null)
        {
            // No merge base - return all commits
            var startTime = DateTimeOffset.UtcNow;
            var commitList = branch.Commits.ToList();
            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;

            logger.LogInformation(
                "Enumerated {CommitCount} commits for branch {BranchName} in {ElapsedSeconds:F1}s",
                commitList.Count,
                branchName,
                elapsed);

            commits = commitList;
        }
        else
        {
            // Manually filter commits: stop at merge base
            commits = branch.Commits.TakeWhile(c => c.Sha != sinceCommitSha);
        }

        // Map to domain commits
        var startMapping = DateTimeOffset.UtcNow;
        var result = commits.Select(c => MapCommitFast(c, branchName)).ToList() as IReadOnlyList<DomainCommit>;
        var mappingElapsed = (DateTimeOffset.UtcNow - startMapping).TotalSeconds;

        logger.LogInformation(
            "Mapped {CommitCount} commits for branch {BranchName} in {ElapsedSeconds:F1}s",
            result.Count,
            branchName,
            mappingElapsed);

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
