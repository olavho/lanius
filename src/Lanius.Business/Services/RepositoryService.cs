using Lanius.Business.Configuration;
using Lanius.Business.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Lanius.Business.Services;

/// <summary>
/// Service for Git repository operations using LibGit2Sharp.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly RepositoryStorageOptions _options;
    private readonly ILogger<RepositoryService>? _logger;

    public RepositoryService(IOptions<RepositoryStorageOptions> options, ILogger<RepositoryService>? logger = null)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.BasePath))
        {
            throw new InvalidOperationException(
                "Repository storage BasePath is not configured. Please set RepositoryStorage:BasePath in appsettings.json");
        }

        _logger?.LogInformation("Initializing RepositoryService with BasePath: {BasePath}", _options.BasePath);
        EnsureBasePathExists();
    }

    public async Task<RepositoryInfo> CloneRepositoryAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        _logger?.LogInformation("Starting clone operation for URL: {Url}", url);

        var repoId = GenerateRepositoryId(url);
        var localPath = GetRepositoryPath(repoId);

        _logger?.LogInformation("Generated repository ID: {RepoId}, Local path: {LocalPath}", repoId, localPath);

        // Check if repository already exists
        if (Directory.Exists(localPath) && Repository.IsValid(localPath))
        {
            _logger?.LogInformation("Repository already exists at: {LocalPath}. Fetching updates instead.", localPath);

            try
            {
                // Fetch updates from remote
                await FetchUpdatesAsync(repoId, cancellationToken);
                _logger?.LogInformation("Successfully fetched updates for existing repository: {RepoId}", repoId);
            }
            catch (Exception fetchEx)
            {
                _logger?.LogWarning(fetchEx, "Failed to fetch updates for existing repository, returning existing info");
            }

            // Return existing repository info
            var existingInfo = await GetRepositoryInfoAsync(repoId);
            if (existingInfo != null)
            {
                return existingInfo;
            }
        }

        // Clean up invalid/partial directory if it exists
        if (Directory.Exists(localPath))
        {
            _logger?.LogWarning("Directory exists but is not a valid repository, cleaning up: {LocalPath}", localPath);
            try
            {
                RemoveReadOnlyAttributes(localPath);
                Directory.Delete(localPath, recursive: true);
            }
            catch (Exception cleanupEx)
            {
                _logger?.LogError(cleanupEx, "Failed to cleanup invalid repository directory");
                throw new InvalidOperationException(
                    $"Repository directory exists but is invalid and cannot be cleaned up: {localPath}", cleanupEx);
            }
        }

        try
        {
            _logger?.LogInformation("Calling LibGit2Sharp Repository.Clone...");

            await Task.Run(() =>
            {
                var cloneOptions = new CloneOptions
                {
                    Checkout = true,
                    RecurseSubmodules = false
                };

                // Add default credentials provider
                cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                        new DefaultCredentials();

                Repository.Clone(url, localPath, cloneOptions);

                _logger?.LogInformation("Clone completed successfully");
            }, cancellationToken);

            return await GetRepositoryInfoAsync(repoId)
                ?? throw new InvalidOperationException("Failed to retrieve repository info after clone");
        }
        catch (LibGit2SharpException ex)
        {
            _logger?.LogError(ex, "LibGit2Sharp exception during clone. Message: {Message}, InnerException: {Inner}",
                ex.Message, ex.InnerException?.Message);

            // Clean up partial clone if it exists
            if (Directory.Exists(localPath))
            {
                try
                {
                    _logger?.LogInformation("Cleaning up partial clone at: {LocalPath}", localPath);
                    RemoveReadOnlyAttributes(localPath);
                    Directory.Delete(localPath, recursive: true);
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogWarning(cleanupEx, "Failed to cleanup partial clone");
                }
            }

            // Wrap LibGit2Sharp exceptions with more user-friendly messages
            throw new InvalidOperationException($"Failed to clone repository: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected exception during clone: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<bool> FetchUpdatesAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);

        var localPath = GetRepositoryPath(repositoryId);
        if (!Repository.IsValid(localPath))
        {
            throw new InvalidOperationException($"Repository not found: {repositoryId}");
        }

        try
        {
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
                var fetchOptions = new FetchOptions();
                fetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                    new DefaultCredentials();

                Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, null);

                var refsAfter = repo.Refs.Count();
                return refsAfter > refsBefore;
            }, cancellationToken);
        }
        catch (LibGit2SharpException ex)
        {
            _logger?.LogError(ex, "Failed to fetch updates for repository: {RepositoryId}", repositoryId);
            throw new InvalidOperationException($"Failed to fetch updates: {ex.Message}", ex);
        }
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
            _logger?.LogInformation("Creating base path: {BasePath}", _options.BasePath);
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
