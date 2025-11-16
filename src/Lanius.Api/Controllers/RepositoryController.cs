using Lanius.Api.DTOs;
using Lanius.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<RepositoryController> _logger;

    public RepositoryController(
        IRepositoryService repositoryService,
        ILogger<RepositoryController> logger)
    {
        _repositoryService = repositoryService;
        _logger = logger;
    }

    /// <summary>
    /// Clone a Git repository.
    /// </summary>
    /// <param name="request">Repository clone request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Repository information.</returns>
    [HttpPost("clone")]
    [ProducesResponseType(typeof(RepositoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RepositoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RepositoryResponse>> CloneRepository(
        [FromBody] CloneRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Cloning repository from URL: {Url}", request.Url);

            // Check if repository already exists before cloning
            var repoId = GenerateRepositoryId(request.Url);
            var alreadyExisted = _repositoryService.RepositoryExists(repoId);

            var info = await _repositoryService.CloneRepositoryAsync(request.Url, cancellationToken);

            var response = new RepositoryResponse
            {
                Id = info.Id,
                Url = info.Url,
                DefaultBranch = info.DefaultBranch,
                ClonedAt = info.ClonedAt,
                LastFetchedAt = info.LastFetchedAt,
                TotalCommits = info.TotalCommits,
                TotalBranches = info.TotalBranches,
                AlreadyExisted = alreadyExisted
            };

            // Return 200 OK if repository already existed, 201 Created if newly cloned
            if (alreadyExisted)
            {
                _logger.LogInformation("Repository already existed, fetched updates: {Id}", info.Id);
                return Ok(response);
            }

            return CreatedAtAction(nameof(GetRepository), new { id = info.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to clone repository: {Url}", request.Url);
            return BadRequest(new ErrorResponse
            {
                Error = "CloneFailed",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error cloning repository: {Url}", request.Url);
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalError",
                Message = "An unexpected error occurred while cloning the repository",
                Detail = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Get repository information by ID.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <returns>Repository information.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RepositoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RepositoryResponse>> GetRepository(string id)
    {
        var info = await _repositoryService.GetRepositoryInfoAsync(id);

        if (info == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = $"Repository with ID '{id}' not found",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return Ok(new RepositoryResponse
        {
            Id = info.Id,
            Url = info.Url,
            DefaultBranch = info.DefaultBranch,
            ClonedAt = info.ClonedAt,
            LastFetchedAt = info.LastFetchedAt,
            TotalCommits = info.TotalCommits,
            TotalBranches = info.TotalBranches
        });
    }

    /// <summary>
    /// Fetch updates from the remote repository.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if new commits were fetched.</returns>
    [HttpPost("{id}/fetch")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> FetchUpdates(
        string id,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching updates for repository: {Id}", id);

            var hasUpdates = await _repositoryService.FetchUpdatesAsync(id, cancellationToken);

            return Ok(hasUpdates);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Repository not found: {Id}", id);
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Delete a repository.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRepository(string id)
    {
        _logger.LogInformation("Deleting repository: {Id}", id);

        await _repositoryService.DeleteRepositoryAsync(id);

        return NoContent();
    }

    private static string GenerateRepositoryId(string url)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
