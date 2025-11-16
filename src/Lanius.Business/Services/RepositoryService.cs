using System.Security.Cryptography;
using System.Text;
using Lanius.Business.Configuration;
using Lanius.Business.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Options;

namespace Lanius.Business.Services;

/// <summary>
/// Service for Git repository operations using LibGit2Sharp.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly RepositoryStorageOptions _options;

    public RepositoryService(IOptions<RepositoryStorageOptions> options)
    {
        _options = options.Value;
        EnsureBasePathExists();
    }

    public async Task<RepositoryInfo> CloneRepositoryAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var repoId = GenerateRepositoryId(url);
        var localPath = GetRepositoryPath(repoId);

        if (Directory.Exists(localPath))
        {
            throw new InvalidOperationException($"Repository already exists at {localPath}");
        }

        await Task.Run(() =>
        {
            var cloneOptions = new CloneOptions
            {
                Checkout = true,
                RecurseSubmodules = false
            };

            Repository.Clone(url, localPath, cloneOptions);
        }, cancellationToken);

        return await GetRepositoryInfoAsync(repoId) 
            ?? throw new InvalidOperationException("Failed to retrieve repository info after clone");
    }

    public async Task<bool> FetchUpdatesAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        var localPath = GetRepositoryPath(repositoryId);
        if (!Repository.IsValid(localPath))
        {
            throw new InvalidOperationException($"Repository not found: {repositoryId}");
        }

        return await Task.Run(() =>
        {
            using var repo = new Repository(localPath);
            var remote = repo.Network.Remotes["origin"];
            if (remote == null)
            {
                return false;
            }

            var refsBefore = repo.Refs.Count();

            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, null, null);

            var refsAfter = repo.Refs.Count();
            return refsAfter > refsBefore;
        }, cancellationToken);
    }

    public Task<RepositoryInfo?> GetRepositoryInfoAsync(string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        var localPath = GetRepositoryPath(repositoryId);
        if (!Repository.IsValid(localPath))
        {
            return Task.FromResult<RepositoryInfo?>(null);
        }

        using var repo = new Repository(localPath);
        
        var defaultBranch = repo.Head.FriendlyName;
        var totalCommits = repo.Commits.Count();
        var totalBranches = repo.Branches.Count();

        // Try to get clone timestamp from .git directory creation time
        var gitDir = Path.Combine(localPath, ".git");
        var clonedAt = Directory.Exists(gitDir) 
            ? Directory.GetCreationTimeUtc(gitDir) 
            : DateTimeOffset.UtcNow;

        // Get URL from remote origin
        var url = repo.Network.Remotes["origin"]?.Url ?? string.Empty;

        var info = new RepositoryInfo
        {
            Id = repositoryId,
            Url = url,
            LocalPath = localPath,
            DefaultBranch = defaultBranch,
            ClonedAt = clonedAt,
            LastFetchedAt = null, // TODO: Track this separately
            TotalCommits = totalCommits,
            TotalBranches = totalBranches
        };

        return Task.FromResult<RepositoryInfo?>(info);
    }

    public bool RepositoryExists(string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        
        var localPath = GetRepositoryPath(repositoryId);
        return Repository.IsValid(localPath);
    }

    public Task DeleteRepositoryAsync(string repositoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        var localPath = GetRepositoryPath(repositoryId);
        if (!Directory.Exists(localPath))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            // Remove read-only attributes from .git folder
            RemoveReadOnlyAttributes(localPath);
            Directory.Delete(localPath, recursive: true);
        });
    }

    private string GenerateRepositoryId(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private string GetRepositoryPath(string repositoryId)
    {
        return Path.Combine(_options.BasePath, repositoryId);
    }

    private void EnsureBasePathExists()
    {
        if (!Directory.Exists(_options.BasePath))
        {
            Directory.CreateDirectory(_options.BasePath);
        }
    }

    private static void RemoveReadOnlyAttributes(string path)
    {
        var dirInfo = new DirectoryInfo(path);
        dirInfo.Attributes &= ~FileAttributes.ReadOnly;

        foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes &= ~FileAttributes.ReadOnly;
        }
    }
}
