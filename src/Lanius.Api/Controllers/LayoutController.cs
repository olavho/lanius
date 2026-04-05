using Lanius.Api.DTOs;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/repository/{repositoryId}/[controller]")]
public class LayoutController(
    ILayoutEngine layoutEngine,
    ILogger<LayoutController> logger) : ControllerBase
{
    /// <summary>
    /// Calculate layout for a repository.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="mode">Layout mode (Logical or Calendar). Default: Logical.</param>
    /// <param name="granularity">Calendar granularity (Day, Week, Month, Year). Only used for Calendar mode.</param>
    /// <param name="branchFilter">Comma-separated branch patterns to filter (e.g., "main,develop,release/*").</param>
    /// <param name="canvasWidth">Canvas width in pixels. Default: 1200.</param>
    /// <param name="canvasHeight">Canvas height in pixels. Default: 600.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Layout result with node positions and edge connections.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(LayoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LayoutResponse>> GetLayout(
        string repositoryId,
        [FromQuery] LayoutMode mode = LayoutMode.Logical,
        [FromQuery] CalendarGranularity granularity = CalendarGranularity.Month,
        [FromQuery] string? branchFilter = null,
        [FromQuery] double canvasWidth = 1200,
        [FromQuery] double canvasHeight = 600,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Calculating {Mode} layout for repository {RepositoryId}",
                mode, repositoryId);

            var options = new LayoutOptions
            {
                Mode = mode,
                Granularity = granularity,
                BranchFilter = branchFilter,
                CanvasWidth = canvasWidth,
                CanvasHeight = canvasHeight
            };

            var result = await layoutEngine.CalculateLayoutAsync(
                repositoryId,
                options,
                progress: null, // TODO: Add SignalR progress reporting
                cancellationToken);

            var response = new LayoutResponse
            {
                Mode = result.Mode,
                Nodes = [.. result.Nodes.Select(n => new LayoutNodeDto
                {
                    CommitId = n.CommitId,
                    X = n.X,
                    Y = n.Y,
                    Radius = n.Radius,
                    BranchName = n.BranchName,
                    Timestamp = n.Timestamp,
                    Message = n.Message,
                    IsSignificant = n.IsSignificant
                })],
                Edges = [.. result.Edges.Select(e => new LayoutEdgeDto
                {
                    FromCommitId = e.FromCommitId,
                    ToCommitId = e.ToCommitId,
                    Type = e.Type,
                    BranchName = e.BranchName ?? string.Empty,
                    Points = e?.Points?.Select(p => (p[0], p[1])).ToList()
                })],
                Width = result.Width,
                Height = result.Height,
                MinTimestamp = result.MinTimestamp,
                MaxTimestamp = result.MaxTimestamp,
                TotalCommits = result.TotalCommits,
                TotalBranches = result.TotalBranches
            };

            logger.LogInformation(
                "Layout calculated: {TotalCommits} commits, {TotalBranches} branches, {NodeCount} nodes, {EdgeCount} edges",
                response.TotalCommits, response.TotalBranches, response.Nodes.Count, response.Edges.Count);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid layout request for repository {RepositoryId}", repositoryId);
            return BadRequest(new ErrorResponse
            {
                Error = "BadRequest",
                Message = ex.Message,
                Detail = "Invalid layout parameters",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating layout for repository {RepositoryId}", repositoryId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to calculate layout",
                Detail = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
