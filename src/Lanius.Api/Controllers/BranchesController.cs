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

    /// <summary>
    /// Get simplified branch overview with only significant commits (heads and merge bases).
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="patterns">Optional branch name patterns (e.g., "main", "release/*").</param>
    /// <param name="includeRemote">Include remote branches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Simplified branch overview.</returns>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(BranchOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchOverviewResponse>> GetBranchOverview(
        string repositoryId,
        [FromQuery] string[]? patterns = null,
        [FromQuery] bool includeRemote = true, // Changed default to true
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting branch overview for repository: {Id}, patterns: {Patterns}, includeRemote: {IncludeRemote}", 
                repositoryId, patterns != null ? string.Join(", ", patterns) : "none", includeRemote);

            // Get filtered branches
            IReadOnlyList<Business.Models.Branch> branches;
            if (patterns != null && patterns.Length > 0)
            {
                _logger.LogInformation("Using pattern-based filtering with {Count} patterns", patterns.Length);
                branches = await _branchAnalyzer.GetBranchesByPatternAsync(
                    repositoryId, patterns, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Loading all branches (includeRemote: {IncludeRemote})", includeRemote);
                branches = await _branchAnalyzer.GetBranchesAsync(
                    repositoryId, includeRemote, cancellationToken);
            }

            _logger.LogInformation("Found {Count} branches: {Names}", 
                branches.Count, string.Join(", ", branches.Select(b => b.Name).Take(10)));

            if (branches.Count == 0)
            {
                return Ok(new BranchOverviewResponse
                {
                    Branches = new List<BranchSummary>(),
                    SignificantCommits = new List<SignificantCommit>(),
                    Relationships = new List<CommitRelationship>()
                });
            }

            // Limit to reasonable number of branches for performance (e.g., top 20)
            var limitedBranches = branches.Take(20).ToList();
            if (branches.Count > 20)
            {
                _logger.LogInformation("Limiting from {Total} to {Limited} branches for performance", 
                    branches.Count, limitedBranches.Count);
            }

            // Get branch names
            var branchNames = limitedBranches.Select(b => b.Name).ToList();

            // Get the simplified overview
            var overview = await _branchAnalyzer.GetBranchOverviewAsync(
                repositoryId, branchNames, cancellationToken);

            _logger.LogInformation("Overview generated: {CommitCount} significant commits, {RelationshipCount} relationships",
                overview.SignificantCommits.Count, overview.Relationships.Count);

            // Map to DTOs
            var branchSummaries = overview.Branches.Select(b => new BranchSummary
            {
                Name = b.Name,
                HeadSha = b.HeadSha,
                HeadTimestamp = b.HeadTimestamp
            }).ToList();

            var significantCommits = overview.SignificantCommits.Select(c => new SignificantCommit
            {
                Sha = c.Sha,
                Author = c.Author,
                Timestamp = c.Timestamp,
                ShortMessage = c.ShortMessage,
                Branches = c.Branches,
                Type = c.Significance switch
                {
                    Business.Models.CommitSignificance.BranchHead => SignificantCommitType.BranchHead,
                    Business.Models.CommitSignificance.MergeBase => SignificantCommitType.MergeBase,
                    Business.Models.CommitSignificance.Both => SignificantCommitType.Both,
                    _ => SignificantCommitType.BranchHead
                },
                Stats = c.Stats != null ? new DiffStatsResponse
                {
                    LinesAdded = c.Stats.LinesAdded,
                    LinesRemoved = c.Stats.LinesRemoved,
                    TotalChanges = c.Stats.TotalChanges,
                    NetChange = c.Stats.NetChange,
                    FilesChanged = c.Stats.FilesChanged,
                    ColorIndicator = c.Stats.ColorIndicator
                } : null
            }).ToList();

            var relationships = overview.Relationships.Select(r => new CommitRelationship
            {
                CommitSha = r.CommitSha,
                Branch1 = r.Branch1,
                Branch2 = r.Branch2,
                Type = RelationshipType.MergeBase
            }).ToList();

            return Ok(new BranchOverviewResponse
            {
                Branches = branchSummaries,
                SignificantCommits = significantCommits,
                Relationships = relationships
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
}
