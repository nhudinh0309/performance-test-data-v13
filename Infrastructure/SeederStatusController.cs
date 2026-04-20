namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// API controller for checking seeder status and member test configuration.
/// GET /umbraco/api/seederstatus/status
/// GET /umbraco/api/seederstatus/members
/// </summary>
public class SeederStatusController : UmbracoApiController
{
    private readonly SeederStatusService _statusService;
    private readonly SeederConfiguration _config;
    private readonly SeederOptions _options;

    /// <summary>
    /// Creates a new SeederStatusController instance.
    /// </summary>
    public SeederStatusController(
        SeederStatusService statusService,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options)
    {
        _statusService = statusService;
        _config = config.Value;
        _options = options.Value;
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

    /// <summary>
    /// Returns member test configuration for k6 load testing scripts.
    /// </summary>
    [HttpGet("members")]
    public IActionResult GetMemberConfig()
    {
        var memberPrefix = _options.Prefixes.Member;
        var memberCount = _config.Members.Count;
        var password = _config.Members.DefaultPassword;

        return Ok(new
        {
            MemberPrefix = memberPrefix,
            MemberCount = memberCount,
            DefaultPassword = password,
            EmailDomain = "example.com",
            LoginUrl = "/umbraco/api/memberauth/login",
            LogoutUrl = "/umbraco/api/memberauth/logout",
            MeUrl = "/umbraco/api/memberauth/me",
            ContactFormUrl = "/umbraco/api/contactform/submit"
        });
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
