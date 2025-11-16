using Lanius.Api.DTOs;
using Lanius.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lanius.Api.Controllers;

[ApiController]
[Route("api/monitoring")]
public class MonitoringController : ControllerBase
{
    private readonly RepositoryMonitoringService _monitoringService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        RepositoryMonitoringService monitoringService,
        ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }

    /// <summary>
    /// Start monitoring a repository for updates.
    /// </summary>
    /// <param name="repositoryId">Repository ID to monitor.</param>
    /// <returns>Success status.</returns>
    [HttpPost("start/{repositoryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult StartMonitoring(string repositoryId)
    {
        _logger.LogInformation("Starting monitoring for repository: {RepositoryId}", repositoryId);
        
        _monitoringService.MonitorRepository(repositoryId);
        
        return Ok(new { message = "Monitoring started", repositoryId });
    }

    /// <summary>
    /// Stop monitoring a repository.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <returns>Success status.</returns>
    [HttpPost("stop/{repositoryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult StopMonitoring(string repositoryId)
    {
        _logger.LogInformation("Stopping monitoring for repository: {RepositoryId}", repositoryId);
        
        _monitoringService.StopMonitoring(repositoryId);
        
        return Ok(new { message = "Monitoring stopped", repositoryId });
    }
}
