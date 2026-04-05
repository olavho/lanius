using Lanius.Api.DTOs;
using Lanius.Api.Hubs;
using Lanius.Business.Configuration;
using Lanius.Business.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Lanius.Api.Services;

/// <summary>
/// Background service that monitors repositories for new commits and broadcasts updates via SignalR.
/// </summary>
public class RepositoryMonitoringService(
    IServiceProvider serviceProvider,
    IHubContext<RepositoryHub> hubContext,
    IOptions<MonitoringOptions> options,
    ILogger<RepositoryMonitoringService> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;
    private readonly Dictionary<string, string> _lastKnownCommits = [];
    private readonly HashSet<string> _monitoredRepositories = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Add a repository to monitor.
    /// </summary>
    public void MonitorRepository(string repositoryId)
    {
        lock (_lock)
        {
            if (_monitoredRepositories.Add(repositoryId))
            {
                logger.LogInformation("Started monitoring repository: {RepositoryId}", repositoryId);
            }
        }
    }

    /// <summary>
    /// Stop monitoring a repository.
    /// </summary>
    public void StopMonitoring(string repositoryId)
    {
        lock (_lock)
        {
            if (_monitoredRepositories.Remove(repositoryId))
            {
                _lastKnownCommits.Remove(repositoryId);
                logger.LogInformation("Stopped monitoring repository: {RepositoryId}", repositoryId);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Repository monitoring is disabled");
            return;
        }

        logger.LogInformation("Repository monitoring service started with {Interval} polling interval",
            _options.PollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdates(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking for repository updates");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }

    private async Task CheckForUpdates(CancellationToken cancellationToken)
    {
        string[] repositoriesToCheck;

        lock (_lock)
        {
            repositoriesToCheck = [.. _monitoredRepositories];
        }

        if (repositoriesToCheck.Length == 0)
        {
            return;
        }

        logger.LogDebug("Checking {Count} repositories for updates", repositoriesToCheck.Length);

        foreach (var repositoryId in repositoriesToCheck)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await CheckRepositoryForUpdates(repositoryId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error checking repository {RepositoryId} for updates", repositoryId);
            }
        }
    }

    private async Task CheckRepositoryForUpdates(string repositoryId, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var repositoryService = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var commitAnalyzer = scope.ServiceProvider.GetRequiredService<ICommitAnalyzer>();

        // Fetch latest changes from remote
        var hasUpdates = await repositoryService.FetchUpdatesAsync(repositoryId, cancellationToken);

        if (!hasUpdates)
        {
            return;
        }

        logger.LogInformation("Repository {RepositoryId} has new commits", repositoryId);

        // Get latest commits
        var commits = await commitAnalyzer.GetCommitsAsync(repositoryId, null, cancellationToken);

        if (commits.Count == 0)
        {
            return;
        }

        var latestCommitSha = commits[0].Sha;

        // Check if this is a new commit we haven't seen before
        string? previousCommitSha;
        lock (_lock)
        {
            _lastKnownCommits.TryGetValue(repositoryId, out previousCommitSha);
            _lastKnownCommits[repositoryId] = latestCommitSha;
        }

        if (previousCommitSha == null)
        {
            // First time checking this repository
            logger.LogDebug("Initialized tracking for repository {RepositoryId}", repositoryId);
            return;
        }

        if (previousCommitSha == latestCommitSha)
        {
            // No new commits
            return;
        }

        // Find new commits since last check
        var newCommits = commits
            .TakeWhile(c => c.Sha != previousCommitSha)
            .Select(c => new CommitResponse
            {
                Sha = c.Sha,
                Author = c.Author,
                AuthorEmail = c.AuthorEmail,
                Timestamp = c.Timestamp,
                Message = c.Message,
                ShortMessage = c.ShortMessage,
                ParentShas = [.. c.ParentShas],
                IsMerge = c.IsMerge,
                Stats = c.Stats != null ? new DiffStatsResponse
                {
                    LinesAdded = c.Stats.LinesAdded,
                    LinesRemoved = c.Stats.LinesRemoved,
                    TotalChanges = c.Stats.TotalChanges,
                    NetChange = c.Stats.NetChange,
                    FilesChanged = c.Stats.FilesChanged,
                    ColorIndicator = c.Stats.ColorIndicator
                } : null,
                Branches = [.. c.Branches]
            })
            .ToList();

        if (newCommits.Count > 0)
        {
            logger.LogInformation("Broadcasting {Count} new commits for repository {RepositoryId}",
                newCommits.Count, repositoryId);

            // Broadcast to all connected clients subscribed to this repository
            await hubContext.Clients.Group($"repo:{repositoryId}")
                .SendAsync("ReceiveNewCommits", newCommits, cancellationToken);

            // Also broadcast repository update
            var repoInfo = await repositoryService.GetRepositoryInfoAsync(repositoryId);
            if (repoInfo != null)
            {
                await hubContext.Clients.Group($"repo:{repositoryId}")
                    .SendAsync("RepositoryUpdated", new RepositoryResponse
                    {
                        Id = repoInfo.Id,
                        Url = repoInfo.Url,
                        DefaultBranch = repoInfo.DefaultBranch,
                        ClonedAt = repoInfo.ClonedAt,
                        LastFetchedAt = repoInfo.LastFetchedAt,
                        TotalCommits = repoInfo.TotalCommits,
                        TotalBranches = repoInfo.TotalBranches
                    }, cancellationToken);
            }
        }
    }
}
