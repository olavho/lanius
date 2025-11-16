using Lanius.Business.Models;
using LibGit2Sharp;
using GitBranch = LibGit2Sharp.Branch;
using DomainBranch = Lanius.Business.Models.Branch;

namespace Lanius.Business.Services;

/// <summary>
/// Service for analyzing Git branches using LibGit2Sharp.
/// </summary>
public class BranchAnalyzer : IBranchAnalyzer
{
    private readonly IRepositoryService _repositoryService;

    public BranchAnalyzer(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    public async Task<IReadOnlyList<DomainBranch>> GetBranchesAsync(
        string repositoryId,
        bool includeRemote = true,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var repo = OpenRepository(repositoryId);

            var branches = repo.Branches
                .Where(b => includeRemote || !b.IsRemote)
                .Select(b => MapBranch(b, repo))
                .ToList();

            return branches as IReadOnlyList<DomainBranch>;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DomainBranch>> GetBranchesByPatternAsync(
        string repositoryId,
        IEnumerable<string> patterns,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var repo = OpenRepository(repositoryId);

            var patternList = patterns.ToList();
            var branches = repo.Branches
                .Where(b => MatchesAnyPattern(b.FriendlyName, patternList))
                .Select(b => MapBranch(b, repo))
                .ToList();

            return branches as IReadOnlyList<DomainBranch>;
        }, cancellationToken);
    }

    public Task<DomainBranch?> GetBranchAsync(string repositoryId, string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        using var repo = OpenRepository(repositoryId);
        var branch = repo.Branches[branchName];

        if (branch == null)
        {
            return Task.FromResult<DomainBranch?>(null);
        }

        // Map the branch while repo is still open
        var result = MapBranch(branch, repo);
        return Task.FromResult<DomainBranch?>(result);
    }

    public async Task<(int ahead, int behind)> GetBranchDivergenceAsync(
        string repositoryId,
        string baseBranch,
        string compareBranch)
    {
        return await Task.Run(() =>
        {
            using var repo = OpenRepository(repositoryId);

            var baseRef = repo.Branches[baseBranch]
                ?? throw new InvalidOperationException($"Base branch not found: {baseBranch}");

            var compareRef = repo.Branches[compareBranch]
                ?? throw new InvalidOperationException($"Compare branch not found: {compareBranch}");

            var divergence = repo.ObjectDatabase.CalculateHistoryDivergence(
                compareRef.Tip,
                baseRef.Tip);

            return (divergence.AheadBy ?? 0, divergence.BehindBy ?? 0);
        });
    }

    public async Task<string?> FindCommonAncestorAsync(
        string repositoryId,
        string branch1,
        string branch2)
    {
        return await Task.Run(() =>
        {
            using var repo = OpenRepository(repositoryId);

            var b1 = repo.Branches[branch1]
                ?? throw new InvalidOperationException($"Branch not found: {branch1}");

            var b2 = repo.Branches[branch2]
                ?? throw new InvalidOperationException($"Branch not found: {branch2}");

            var mergeBase = repo.ObjectDatabase.FindMergeBase(b1.Tip, b2.Tip);
            return mergeBase?.Sha;
        });
    }

    private Repository OpenRepository(string repositoryId)
    {
        if (!_repositoryService.RepositoryExists(repositoryId))
        {
            throw new InvalidOperationException($"Repository not found: {repositoryId}");
        }

        var info = _repositoryService.GetRepositoryInfoAsync(repositoryId).Result;
        return new Repository(info!.LocalPath);
    }

    private DomainBranch MapBranch(GitBranch gitBranch, Repository repo)
    {
        var upstreamBranch = gitBranch.TrackedBranch?.FriendlyName;
        int? commitsAhead = null;
        int? commitsBehind = null;

        if (gitBranch.TrackedBranch != null)
        {
            var divergence = repo.ObjectDatabase.CalculateHistoryDivergence(
                gitBranch.Tip,
                gitBranch.TrackedBranch.Tip);

            commitsAhead = divergence.AheadBy;
            commitsBehind = divergence.BehindBy;
        }

        return new DomainBranch
        {
            Name = gitBranch.FriendlyName,
            FullName = gitBranch.CanonicalName,
            TipSha = gitBranch.Tip.Sha,
            IsRemote = gitBranch.IsRemote,
            RemoteName = gitBranch.IsRemote ? gitBranch.RemoteName : null,
            IsHead = gitBranch.IsCurrentRepositoryHead,
            UpstreamBranch = upstreamBranch,
            CommitsAhead = commitsAhead,
            CommitsBehind = commitsBehind
        };
    }

    private static bool MatchesAnyPattern(string branchName, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(branchName, pattern))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesPattern(string branchName, string pattern)
    {
        // Simple wildcard matching: "release/*" matches "release/1.0", "release/2.0"
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            return branchName.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }

        // Exact match
        return branchName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
 