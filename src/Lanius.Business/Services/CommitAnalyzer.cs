using LibGit2Sharp;
using DomainCommit = Lanius.Business.Models.Commit;
using GitCommit = LibGit2Sharp.Commit;

namespace Lanius.Business.Services;

/// <summary>
/// Service for analyzing Git commits using LibGit2Sharp.
/// </summary>
public class CommitAnalyzer(IRepositoryService repositoryService) : ICommitAnalyzer
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
        CancellationToken cancellationToken = default)
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

            return commits.Select(c => MapCommit(c, repo)).ToList() as IReadOnlyList<DomainCommit>;
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
            using var repo = OpenRepository(repositoryId);

            var branch = repo.Branches[branchName]
                ?? throw new InvalidOperationException($"Branch not found: {branchName}");

            IEnumerable<GitCommit> commits;

            if (sinceCommitSha == null)
            {
                // No merge base - return all commits
                commits = branch.Commits;
            }
            else
            {
                // Manually filter commits: stop at merge base
                commits = branch.Commits.TakeWhile(c => c.Sha != sinceCommitSha);
            }

            // Map to domain commits
            return commits.Select(c => MapCommit(c, repo)).ToList() as IReadOnlyList<DomainCommit>;

        }, cancellationToken);
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
