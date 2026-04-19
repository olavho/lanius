using Lanius.Api.DTOs;
using Lanius.Api.Hubs;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/repository/{repositoryId}/[controller]")]
public class LayoutController(
    IServiceProvider serviceProvider,
    IHubContext<RepositoryHub> hubContext,
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
        [FromQuery] CalendarGranularity? granularity = null,
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

            // Resolve the appropriate layout engine based on mode
            ILayoutEngine layoutEngine = mode switch
            {
                LayoutMode.Logical => serviceProvider.GetRequiredService<LogicalLayoutEngine>(),
                LayoutMode.Calendar => serviceProvider.GetRequiredService<CalendarLayoutEngine>(),
                LayoutMode.Timeline => serviceProvider.GetRequiredService<TimelineLayoutEngine>(),
                _ => throw new ArgumentException($"Unsupported layout mode: {mode}")
            };

            // Collect in-flight SignalR sends so they can be awaited before the HTTP
            // response is sent. Without this, fire-and-forget sends may arrive after
            // the frontend calls hideLayoutProgress() on the HTTP response.
            var progressTasks = new ConcurrentBag<Task>();
            IProgress<LayoutProgress> progress = new LayoutProgressReporter(
                hubContext,
                repositoryId,
                progressTasks);

            var result = await layoutEngine.CalculateLayoutAsync(
                repositoryId,
                options,
                progress: progress,
                cancellationToken);

            if (!progressTasks.IsEmpty)
                await Task.WhenAll(progressTasks);

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
                    Author = n.Author,
                    AuthorEmail = n.AuthorEmail,
                    Committer = n.Committer,
                    CommitterEmail = n.CommitterEmail,
                    CommitterTimestamp = n.CommitterTimestamp,
                    ParentShas = [.. n.ParentShas],
                    IsSignificant = n.IsSignificant,
                    GridRow = n.GridRow,
                    GridColumn = n.GridColumn,
                    IsGhost = n.IsGhost
                })],
                Edges = [.. result.Edges.Select(e => new LayoutEdgeDto
                {
                    FromCommitId = e.FromCommitId,
                    ToCommitId = e.ToCommitId,
                    Type = e.Type,
                    BranchName = e.BranchName ?? string.Empty,
                    X1 = e.X1,
                    Y1 = e.Y1,
                    X2 = e.X2,
                    Y2 = e.Y2,
                    IsVertical = e.IsVertical,
                    Direction = e.Direction.ToString()
                })],
                Width = result.Width,
                Height = result.Height,
                MinTimestamp = result.MinTimestamp,
                MaxTimestamp = result.MaxTimestamp,
                TimelineOriginX = result.TimelineOriginX,
                TimelinePixelsPerSecond = result.TimelinePixelsPerSecond,
                TotalCommits = result.TotalCommits,
                TotalBranches = result.TotalBranches,
                RowCount = result.RowCount,
                ColumnCount = result.ColumnCount
            };

            logger.LogInformation(
                "Layout calculated: {TotalCommits} commits, {TotalBranches} branches, {NodeCount} nodes, {EdgeCount} edges",
                response.TotalCommits, response.TotalBranches, response.Nodes.Count, response.Edges.Count);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Repository not found for layout request {RepositoryId}", repositoryId);
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = ex.Message,
                Detail = "Repository not found",
                Timestamp = DateTimeOffset.UtcNow
            });
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

    /// <summary>
    /// Synchronous progress reporter that records SignalR send tasks deterministically.
    /// </summary>
    private sealed class LayoutProgressReporter(
        IHubContext<RepositoryHub> hubContext,
        string repositoryId,
        ConcurrentBag<Task> progressTasks) : IProgress<LayoutProgress>
    {
        public void Report(LayoutProgress progress)
        {
            var task = hubContext.Clients.Group($"repo:{repositoryId}")
                .SendAsync("LayoutProgress", new
                {
                    percentage = progress.Percentage,
                    operation = progress.Operation,
                    processedItems = progress.ProcessedItems,
                    totalItems = progress.TotalItems
                }, CancellationToken.None);

            progressTasks.Add(task);
        }
    }
}
