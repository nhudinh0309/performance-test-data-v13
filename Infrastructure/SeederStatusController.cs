namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Configuration;

/// <summary>
/// API controller for checking seeder status.
/// GET /umbraco/api/seederstatus/status
/// GET /umbraco/api/seederstatus/members
/// </summary>
[ApiController]
[Route("umbraco/api/seederstatus")]
public class SeederStatusController : ControllerBase
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
    /// Converts a content name to a URL segment matching Umbraco's default convention.
    /// </summary>
    private static string ToUrlSegment(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(" ", "-"), "[^a-z0-9-]", "-").Trim('-');

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
    /// Gets member test configuration for use in load testing scripts.
    /// Returns the prefix, count, and password needed to construct member credentials.
    /// </summary>
    [HttpGet("members")]
    public IActionResult GetMemberConfig()
    {
        var prefix = _options.Prefixes.Member;
        var loginName = $"{prefix}Member Login";
        var memberAreaName = $"{prefix}Member Area";

        // Derive URLs from content names (Umbraco generates URL segments from names)
        var loginPageUrl = $"/{ToUrlSegment(loginName)}/";
        var memberAreaPageUrl = $"/{ToUrlSegment(memberAreaName)}/";

        var response = new MemberTestConfigResponse
        {
            Prefix = prefix,
            Count = _config.Members.Count,
            Password = _config.Members.DefaultPassword,
            EmailDomain = "example.com",
            LoginUrl = "/umbraco/api/memberauth/login",
            LogoutUrl = "/umbraco/api/memberauth/logout",
            MeUrl = "/umbraco/api/memberauth/me",
            ContactFormUrl = "/umbraco/api/contactform/submit",
            ContactFormStatsUrl = "/umbraco/api/contactform/stats",
            ContactFormPageUrl = $"/{ToUrlSegment("Contact Us")}/",
            LoginPageUrl = loginPageUrl,
            MemberAreaPageUrl = memberAreaPageUrl
        };

        return Ok(response);
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

/// <summary>
/// Response model for member test configuration endpoint.
/// Provides all information needed for k6/load test scripts to construct member credentials.
/// </summary>
public class MemberTestConfigResponse
{
    /// <summary>
    /// Username prefix. Members are named {Prefix}1, {Prefix}2, ..., {Prefix}{Count}.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Total number of seeded members.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Password shared by all seeded members.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Email domain. Emails follow the pattern {username_lowercase}@{EmailDomain}.
    /// </summary>
    public string EmailDomain { get; set; } = string.Empty;

    /// <summary>
    /// URL to POST login credentials to (JSON body: { username, password }).
    /// </summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to POST to sign out the current member.
    /// </summary>
    public string LogoutUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to GET the current member's auth state.
    /// </summary>
    public string MeUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to POST contact form data to (JSON body: { name, email, subject, message }).
    /// </summary>
    public string ContactFormUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to GET contact form submission stats.
    /// </summary>
    public string ContactFormStatsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Published URL of the member login page (rendered frontend).
    /// </summary>
    public string? LoginPageUrl { get; set; }

    /// <summary>
    /// Published URL of the member area page (rendered frontend).
    /// </summary>
    public string? MemberAreaPageUrl { get; set; }

    /// <summary>
    /// Published URL of the contact form page (rendered frontend).
    /// </summary>
    public string? ContactFormPageUrl { get; set; }
}
