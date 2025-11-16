using Lanius.Api.DTOs;
using Lanius.Business.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId}/[controller]")]
public class BranchesController : ControllerBase
{
    private readonly IBranchAnalyzer _branchAnalyzer;
    private readonly ILogger<BranchesController> _logger;

    public BranchesController(
        IBranchAnalyzer branchAnalyzer,
        ILogger<BranchesController> logger)
    {
        _branchAnalyzer = branchAnalyzer;
        _logger = logger;
    }

    /// <summary>
    /// Get all branches from a repository.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="includeRemote">Include remote branches.</param>
    /// <param name="patterns">Optional branch name patterns (e.g., "main", "release/*").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of branches.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BranchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<BranchResponse>>> GetBranches(
        string repositoryId,
        [FromQuery] bool includeRemote = true,
        [FromQuery] string[]? patterns = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting branches for repository: {Id}", repositoryId);

            IReadOnlyList<Business.Models.Branch> branches;

            if (patterns != null && patterns.Length > 0)
            {
                branches = await _branchAnalyzer.GetBranchesByPatternAsync(
                    repositoryId, patterns, cancellationToken);
            }
            else
            {
                branches = await _branchAnalyzer.GetBranchesAsync(
                    repositoryId, includeRemote, cancellationToken);
            }

            var response = branches.Select(b => new BranchResponse
            {
                Name = b.Name,
                FullName = b.FullName,
                TipSha = b.TipSha,
                IsRemote = b.IsRemote,
                RemoteName = b.RemoteName,
                IsHead = b.IsHead,
                UpstreamBranch = b.UpstreamBranch,
                CommitsAhead = b.CommitsAhead,
                CommitsBehind = b.CommitsBehind
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
    /// Get a specific branch by name.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="branchName">Branch name.</param>
    /// <returns>Branch information.</returns>
    [HttpGet("{branchName}")]
    [ProducesResponseType(typeof(BranchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchResponse>> GetBranch(string repositoryId, string branchName)
    {
        try
        {
            var branch = await _branchAnalyzer.GetBranchAsync(repositoryId, branchName);

            if (branch == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "BranchNotFound",
                    Message = $"Branch '{branchName}' not found in repository '{repositoryId}'",
                    Timestamp = DateTimeOffset.UtcNow
                });
            }

            return Ok(new BranchResponse
            {
                Name = branch.Name,
                FullName = branch.FullName,
                TipSha = branch.TipSha,
                IsRemote = branch.IsRemote,
                RemoteName = branch.RemoteName,
                IsHead = branch.IsHead,
                UpstreamBranch = branch.UpstreamBranch,
                CommitsAhead = branch.CommitsAhead,
                CommitsBehind = branch.CommitsBehind
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
    /// Get divergence between two branches (commits ahead/behind).
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="baseBranch">Base branch name.</param>
    /// <param name="compareBranch">Branch to compare.</param>
    /// <returns>Divergence information.</returns>
    [HttpGet("divergence")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetBranchDivergence(
        string repositoryId,
        [FromQuery] string baseBranch,
        [FromQuery] string compareBranch)
    {
        try
        {
            _logger.LogInformation("Calculating divergence between {Base} and {Compare}", 
                baseBranch, compareBranch);

            var (ahead, behind) = await _branchAnalyzer.GetBranchDivergenceAsync(
                repositoryId, baseBranch, compareBranch);

            return Ok(new
            {
                baseBranch,
                compareBranch,
                commitsAhead = ahead,
                commitsBehind = behind
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Branch operation failed");
            return NotFound(new ErrorResponse
            {
                Error = "BranchNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Find the common ancestor between two branches.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="branch1">First branch name.</param>
    /// <param name="branch2">Second branch name.</param>
    /// <returns>Common ancestor commit SHA.</returns>
    [HttpGet("common-ancestor")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> FindCommonAncestor(
        string repositoryId,
        [FromQuery] string branch1,
        [FromQuery] string branch2)
    {
        try
        {
            _logger.LogInformation("Finding common ancestor between {Branch1} and {Branch2}", 
                branch1, branch2);

            var ancestorSha = await _branchAnalyzer.FindCommonAncestorAsync(
                repositoryId, branch1, branch2);

            if (ancestorSha == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "NoCommonAncestor",
                    Message = $"No common ancestor found between '{branch1}' and '{branch2}'",
                    Timestamp = DateTimeOffset.UtcNow
                });
            }

            return Ok(new
            {
                branch1,
                branch2,
                commonAncestorSha = ancestorSha
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Branch operation failed");
            return NotFound(new ErrorResponse
            {
                Error = "BranchNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
