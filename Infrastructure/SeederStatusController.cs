namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;

/// <summary>
/// API controller for checking seeder status.
/// GET /umbraco/api/seederstatus/status
/// </summary>
public class SeederStatusController : UmbracoApiController
{
    private readonly SeederStatusService _statusService;

    /// <summary>
    /// Creates a new SeederStatusController instance.
    /// </summary>
    public SeederStatusController(SeederStatusService statusService)
    {
        _statusService = statusService;
    }

    /// <summary>
    /// Gets the current seeder status.
    /// Returns 200 when complete, 202 when running, 503 when failed.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var response = new SeederStatusResponse
        {
            Status = _statusService.Status.ToString(),
            IsComplete = _statusService.Status is SeederStatus.Completed
                or SeederStatus.CompletedWithErrors
                or SeederStatus.Skipped,
            CurrentSeeder = _statusService.CurrentSeeder,
            ExecutedCount = _statusService.ExecutedCount,
            FailedCount = _statusService.FailedCount,
            ElapsedMs = _statusService.ElapsedMs,
            ErrorMessage = _statusService.ErrorMessage
        };

        return _statusService.Status switch
        {
            SeederStatus.Completed => Ok(response),
            SeederStatus.CompletedWithErrors => Ok(response),
            SeederStatus.Skipped => Ok(response),
            SeederStatus.Running => StatusCode(202, response), // Accepted - still processing
            SeederStatus.NotStarted => StatusCode(202, response), // Accepted - waiting to start
            SeederStatus.Failed => StatusCode(503, response), // Service Unavailable
            _ => StatusCode(202, response)
        };
    }
}

/// <summary>
/// Response model for seeder status endpoint.
/// </summary>
public class SeederStatusResponse
{
    /// <summary>
    /// Current status string.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// True if seeding is complete (success, with errors, or skipped).
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Name of the currently running seeder, if any.
    /// </summary>
    public string? CurrentSeeder { get; set; }

    /// <summary>
    /// Number of seeders that executed successfully.
    /// </summary>
    public int ExecutedCount { get; set; }

    /// <summary>
    /// Number of seeders that failed.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Total elapsed time in milliseconds.
    /// </summary>
    public long ElapsedMs { get; set; }

    /// <summary>
    /// Error message if seeding failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
