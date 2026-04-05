using Lanius.Api.DTOs;
using Lanius.Business.Replay.Models;
using Lanius.Business.Replay.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/repositories/{repositoryId}/replay")]
public class ReplayController(
    IReplayService replayService,
    ILogger<ReplayController> logger) : ControllerBase
{

    /// <summary>
    /// Start a new replay session.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="request">Replay configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Replay session information.</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ReplaySessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReplaySessionResponse>> StartReplay(
        string repositoryId,
        [FromBody] StartReplayRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting replay for repository {RepositoryId} with speed {Speed}",
                repositoryId, request.Speed);

            var options = new ReplayOptions
            {
                Speed = request.Speed,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                BranchFilter = request.BranchFilter
            };

            var session = await replayService.StartReplayAsync(repositoryId, options, cancellationToken);

            return Ok(new ReplaySessionResponse
            {
                SessionId = session.SessionId,
                RepositoryId = session.RepositoryId,
                State = session.State.ToString(),
                Speed = session.Options.Speed,
                TotalCommits = session.TotalCommits,
                CurrentIndex = session.CurrentIndex,
                StartedAt = session.StartedAt,
                CompletedAt = session.CompletedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Repository not found: {RepositoryId}", repositoryId);
            return NotFound(new ErrorResponse
            {
                Error = "RepositoryNotFound",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting replay for repository: {RepositoryId}", repositoryId);
            return StatusCode(500, new ErrorResponse
            {
                Error = "ReplayStartFailed",
                Message = "Failed to start replay session",
                Detail = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Pause a replay session.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Success status.</returns>
    [HttpPost("{sessionId}/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult PauseReplay(string repositoryId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        logger.LogInformation("Pausing replay session: {SessionId}", sessionId);

        replayService.PauseReplay(sessionId);

        var session = replayService.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "SessionNotFound",
                Message = $"Replay session '{sessionId}' not found",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return Ok(new { message = "Replay paused", sessionId });
    }

    /// <summary>
    /// Resume a paused replay session.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Success status.</returns>
    [HttpPost("{sessionId}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult ResumeReplay(string repositoryId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        logger.LogInformation("Resuming replay session: {SessionId}", sessionId);

        replayService.ResumeReplay(sessionId);

        var session = replayService.GetSession(sessionId);
        if (session == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "SessionNotFound",
                Message = $"Replay session '{sessionId}' not found",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return Ok(new { message = "Replay resumed", sessionId });
    }

    /// <summary>
    /// Stop a replay session.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Success status.</returns>
    [HttpPost("{sessionId}/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult StopReplay(string repositoryId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        logger.LogInformation("Stopping replay session: {SessionId}", sessionId);

        replayService.StopReplay(sessionId);

        return Ok(new { message = "Replay stopped", sessionId });
    }

    /// <summary>
    /// Adjust playback speed.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="speed">New speed multiplier (e.g., 0.5, 1.0, 2.0).</param>
    /// <returns>Success status.</returns>
    [HttpPost("{sessionId}/speed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult SetSpeed(string repositoryId, string sessionId, [FromBody] double speed)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        if (speed <= 0 || speed > 10)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "InvalidSpeed",
                Message = "Speed must be between 0 and 10",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        logger.LogInformation("Setting replay speed to {Speed} for session: {SessionId}", speed, sessionId);

        replayService.SetSpeed(sessionId, speed);

        return Ok(new { message = "Speed updated", sessionId, speed });
    }

    /// <summary>
    /// Get replay session status.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Session information.</returns>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(ReplaySessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<ReplaySessionResponse> GetSession(string repositoryId, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(repositoryId);
        var session = replayService.GetSession(sessionId);

        if (session == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "SessionNotFound",
                Message = $"Replay session '{sessionId}' not found",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return Ok(new ReplaySessionResponse
        {
            SessionId = session.SessionId,
            RepositoryId = session.RepositoryId,
            State = session.State.ToString(),
            Speed = session.Options.Speed,
            TotalCommits = session.TotalCommits,
            CurrentIndex = session.CurrentIndex,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt
        });
    }
}
