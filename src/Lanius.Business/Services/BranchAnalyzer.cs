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

    public async Task<BranchOverview> GetBranchOverviewAsync(
        string repositoryId,
        IEnumerable<string> branchNames,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var repo = OpenRepository(repositoryId);

            var branchNamesList = branchNames.ToList();
            var branches = new List<GitBranch>();
            
            // Get branch objects
            foreach (var branchName in branchNamesList)
            {
                var branch = repo.Branches[branchName];
                if (branch != null)
                {
                    branches.Add(branch);
                }
            }

            if (branches.Count == 0)
            {
                return new BranchOverview
                {
                    Branches = new List<BranchInfo>(),
                    SignificantCommits = new List<SignificantCommitInfo>(),
                    Relationships = new List<CommitRelation>()
                };
            }

            // Identify main/primary branch (prefer "main", then "master", then first branch)
            var mainBranch = branches.FirstOrDefault(b => b.FriendlyName == "main" || b.FriendlyName == "origin/main")
                          ?? branches.FirstOrDefault(b => b.FriendlyName == "master" || b.FriendlyName == "origin/master")
                          ?? branches.First();

            var significantCommits = new Dictionary<string, SignificantCommitInfo>();
            var relationships = new List<CommitRelation>();

            // Add commits from main branch timeline (limit to recent history for performance)
            var mainCommits = mainBranch.Commits.Take(100).ToList();
            foreach (var commit in mainCommits)
            {
                if (!significantCommits.ContainsKey(commit.Sha))
                {
                    significantCommits[commit.Sha] = CreateSignificantCommit(
                        commit,
                        new List<string> { mainBranch.FriendlyName },
                        CommitSignificance.BranchHead); // Will update if needed
                }
            }

            // Mark the actual head of main branch
            if (mainCommits.Count > 0)
            {
                var headSha = mainBranch.Tip.Sha;
                if (significantCommits.ContainsKey(headSha))
                {
                    significantCommits[headSha].Significance = CommitSignificance.BranchHead;
                }
            }

            // For each other branch, find where it diverges from main and add its head + first commit
            foreach (var branch in branches.Where(b => b != mainBranch))
            {
                // Add branch head
                var branchHead = branch.Tip;
                if (!significantCommits.ContainsKey(branchHead.Sha))
                {
                    significantCommits[branchHead.Sha] = CreateSignificantCommit(
                        branchHead,
                        new List<string> { branch.FriendlyName },
                        CommitSignificance.BranchHead);
                }
                else
                {
                    significantCommits[branchHead.Sha].Branches.Add(branch.FriendlyName);
                }

                // Find merge base (divergence point) with main branch
                try
                {
                    var mergeBase = repo.ObjectDatabase.FindMergeBase(mainBranch.Tip, branch.Tip);
                    
                    if (mergeBase != null)
                    {
                        // Add merge base as significant commit
                        if (!significantCommits.ContainsKey(mergeBase.Sha))
                        {
                            significantCommits[mergeBase.Sha] = CreateSignificantCommit(
                                mergeBase,
                                new List<string> { mainBranch.FriendlyName },
                                CommitSignificance.MergeBase);
                        }
                        else
                        {
                            // This commit is already in main timeline, mark it as a split point
                            var existing = significantCommits[mergeBase.Sha];
                            if (!existing.Branches.Contains(mainBranch.FriendlyName))
                            {
                                existing.Branches.Add(mainBranch.FriendlyName);
                            }
                            if (existing.Significance == CommitSignificance.BranchHead)
                            {
                                existing.Significance = CommitSignificance.Both;
                            }
                            else
                            {
                                existing.Significance = CommitSignificance.MergeBase;
                            }
                        }

                        relationships.Add(new CommitRelation
                        {
                            CommitSha = mergeBase.Sha,
                            Branch1 = mainBranch.FriendlyName,
                            Branch2 = branch.FriendlyName,
                            RelationType = CommitRelationType.MergeBase
                        });

                        // Find the FIRST commit on the branch (immediately after merge base)
                        // Walk from branch head back to merge base and take the commit just after merge base
                        var branchCommits = branch.Commits.TakeWhile(c => c.Sha != mergeBase.Sha).ToList();
                        if (branchCommits.Count > 0)
                        {
                            var firstCommitOnBranch = branchCommits.Last(); // Last in the list = first chronologically
                            
                            if (!significantCommits.ContainsKey(firstCommitOnBranch.Sha))
                            {
                                significantCommits[firstCommitOnBranch.Sha] = CreateSignificantCommit(
                                    firstCommitOnBranch,
                                    new List<string> { branch.FriendlyName },
                                    CommitSignificance.MergeBase); // Mark as significant for visualization
                            }
                            else
                            {
                                // Already exists, just add branch name
                                if (!significantCommits[firstCommitOnBranch.Sha].Branches.Contains(branch.FriendlyName))
                                {
                                    significantCommits[firstCommitOnBranch.Sha].Branches.Add(branch.FriendlyName);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // No common ancestor with main, skip
                    continue;
                }
            }

            // Create branch info list - sort by divergence time (merge base timestamp)
            var branchInfoListUnsorted = branches.Select(b => new BranchInfo
            {
                Name = b.FriendlyName,
                HeadSha = b.Tip.Sha,
                HeadTimestamp = b.Tip.Author.When
            }).ToList();

            // Sort branches by their divergence point (merge base timestamp)
            // Main branch should be first, then others sorted by when they diverged
            var branchInfoList = new List<BranchInfo>();
            
            // Add main branch first
            var mainBranchInfo = branchInfoListUnsorted.FirstOrDefault(b => 
                b.Name == mainBranch.FriendlyName);
            if (mainBranchInfo != null)
            {
                branchInfoList.Add(mainBranchInfo);
            }

            // Sort other branches by their merge base timestamp (when they diverged)
            var otherBranches = branchInfoListUnsorted.Where(b => 
                b.Name != mainBranch.FriendlyName).ToList();
            
            // Create a map of branch -> merge base timestamp
            var branchDivergenceTime = new Dictionary<string, DateTimeOffset>();
            foreach (var rel in relationships)
            {
                if (significantCommits.ContainsKey(rel.CommitSha))
                {
                    var mergeBaseTime = significantCommits[rel.CommitSha].Timestamp;
                    branchDivergenceTime[rel.Branch2] = mergeBaseTime;
                }
            }

            // Sort other branches by divergence time (oldest first)
            var sortedOtherBranches = otherBranches
                .OrderBy(b => branchDivergenceTime.ContainsKey(b.Name) 
                    ? branchDivergenceTime[b.Name] 
                    : DateTimeOffset.MaxValue) // Branches without merge base go last
                .ToList();
            
            branchInfoList.AddRange(sortedOtherBranches);

            return new BranchOverview
            {
                Branches = branchInfoList,
                SignificantCommits = significantCommits.Values.ToList(),
                Relationships = relationships
            };
        }, cancellationToken);
    }

    private SignificantCommitInfo CreateSignificantCommit(
        LibGit2Sharp.Commit commit,
        List<string> branches,
        CommitSignificance significance)
    {
        return new SignificantCommitInfo
        {
            Sha = commit.Sha,
            Author = commit.Author.Name,
            AuthorEmail = commit.Author.Email,
            Timestamp = commit.Author.When,
            Message = commit.Message,
            ShortMessage = commit.MessageShort,
            Branches = branches,
            Significance = significance,
            Stats = null // Stats will be calculated on-demand if needed
        };
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