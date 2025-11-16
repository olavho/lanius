using Lanius.Api.DTOs;
using Lanius.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId}/[controller]")]
public class CommitsController : ControllerBase
{
    private readonly ICommitAnalyzer _commitAnalyzer;
    private readonly ILogger<CommitsController> _logger;

    public CommitsController(
        ICommitAnalyzer commitAnalyzer,
        ILogger<CommitsController> logger)
    {
        _commitAnalyzer = commitAnalyzer;
        _logger = logger;
    }

    /// <summary>
    /// Get all commits from a repository.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="branch">Optional branch filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of commits.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CommitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CommitResponse>>> GetCommits(
        string repositoryId,
        [FromQuery] string? branch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting commits for repository: {Id}, branch: {Branch}", 
                repositoryId, branch ?? "all");

            var commits = await _commitAnalyzer.GetCommitsAsync(repositoryId, branch, cancellationToken);

            var response = commits.Select(c => new CommitResponse
            {
                Sha = c.Sha,
                Author = c.Author,
                AuthorEmail = c.AuthorEmail,
                Timestamp = c.Timestamp,
                Message = c.Message,
                ShortMessage = c.ShortMessage,
                ParentShas = c.ParentShas.ToList(),
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
                Branches = c.Branches.ToList()
            });

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Repository not found: {Id}", repositoryId);
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Get a specific commit by SHA.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="sha">Commit SHA.</param>
    /// <returns>Commit information.</returns>
    [HttpGet("{sha}")]
    [ProducesResponseType(typeof(CommitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitResponse>> GetCommit(string repositoryId, string sha)
    {
        try
        {
            var commit = await _commitAnalyzer.GetCommitAsync(repositoryId, sha);

            if (commit == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "CommitNotFound",
                    Message = $"Commit '{sha}' not found in repository '{repositoryId}'",
                    Timestamp = DateTimeOffset.UtcNow
                });
            }

            return Ok(new CommitResponse
            {
                Sha = commit.Sha,
                Author = commit.Author,
                AuthorEmail = commit.AuthorEmail,
                Timestamp = commit.Timestamp,
                Message = commit.Message,
                ShortMessage = commit.ShortMessage,
                ParentShas = commit.ParentShas.ToList(),
                IsMerge = commit.IsMerge,
                Stats = commit.Stats != null ? new DiffStatsResponse
                {
                    LinesAdded = commit.Stats.LinesAdded,
                    LinesRemoved = commit.Stats.LinesRemoved,
                    TotalChanges = commit.Stats.TotalChanges,
                    NetChange = commit.Stats.NetChange,
                    FilesChanged = commit.Stats.FilesChanged,
                    ColorIndicator = commit.Stats.ColorIndicator
                } : null,
                Branches = commit.Branches.ToList()
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Repository not found: {Id}", repositoryId);
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Get commits in chronological order (for replay mode).
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Commits ordered by timestamp.</returns>
    [HttpGet("chronological")]
    [ProducesResponseType(typeof(IEnumerable<CommitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CommitResponse>>> GetCommitsChronologically(
        string repositoryId,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting chronological commits for repository: {Id}", repositoryId);

            var commits = await _commitAnalyzer.GetCommitsChronologicallyAsync(
                repositoryId, startDate, endDate, cancellationToken);

            var response = commits.Select(c => new CommitResponse
            {
                Sha = c.Sha,
                Author = c.Author,
                AuthorEmail = c.AuthorEmail,
                Timestamp = c.Timestamp,
                Message = c.Message,
                ShortMessage = c.ShortMessage,
                ParentShas = c.ParentShas.ToList(),
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
                Branches = c.Branches.ToList()
            });

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Repository not found: {Id}", repositoryId);
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
