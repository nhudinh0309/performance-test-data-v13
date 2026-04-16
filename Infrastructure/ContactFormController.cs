namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PerformanceTestDataSeeder.Seeders;

/// <summary>
/// Controller for handling contact form submissions during load testing.
/// Saves each submission as an Umbraco content node under "Contact Submissions".
///
/// JSON (k6):
///   POST /umbraco/api/contactform/submit
///   GET  /umbraco/api/contactform/stats
///
/// Form (frontend page):
///   POST /umbraco/api/contactform/form-submit
/// </summary>
[Route("umbraco/api/contactform")]
public class ContactFormController : Controller
{
    private readonly IContentService _contentService;
    private readonly ILogger<ContactFormController> _logger;

    /// <summary>
    /// Creates a new ContactFormController instance.
    /// </summary>
    public ContactFormController(IContentService contentService, ILogger<ContactFormController> logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    /// <summary>
    /// Accepts a contact form submission via JSON. Saves to DB. Returns JSON response.
    /// </summary>
    [HttpPost("submit")]
    public IActionResult Submit([FromBody] ContactFormRequest request)
    {
        if (!IsValid(request, out var error))
        {
            return BadRequest(new ContactFormResponse { Success = false, Error = error });
        }

        try
        {
            SaveSubmission(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save contact form submission from {Name}", request.Name);
            return StatusCode(500, new ContactFormResponse { Success = false, Error = "Failed to save submission" });
        }

        return Ok(new ContactFormResponse
        {
            Success = true,
            Message = $"Thank you {request.Name}, your message has been received."
        });
    }

    /// <summary>
    /// Accepts a contact form submission via form POST. Saves to DB. Redirects back.
    /// </summary>
    [HttpPost("form-submit")]
    public IActionResult FormSubmit([FromForm] ContactFormRequest request)
    {
        if (!IsValid(request, out var error))
        {
            TempData["ContactFormError"] = error;
            return LocalRedirect(request.ReturnUrl ?? "/");
        }

        try
        {
            SaveSubmission(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save contact form submission from {Name}", request.Name);
            TempData["ContactFormError"] = "Failed to save submission. Please try again.";
            return LocalRedirect(request.ReturnUrl ?? "/");
        }

        TempData["ContactFormSuccess"] = $"Thank you {request.Name}, your message has been received.";
        return Redirect(request.ReturnUrl ?? "/");
    }

    /// <summary>
    /// Returns submission count from the database.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult Stats()
    {
        var folderId = GetSubmissionsFolderId();
        if (folderId == null)
        {
            return Ok(new { totalSubmissions = 0 });
        }

        var count = _contentService.CountChildren(folderId.Value);
        return Ok(new { totalSubmissions = count });
    }

    private void SaveSubmission(ContactFormRequest request)
    {
        var folderId = GetSubmissionsFolderId();
        if (folderId == null)
        {
            _logger.LogWarning("Contact submissions folder not found. Has ContactFormSeeder run?");
            return;
        }

        var name = $"{request.Name} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        var content = _contentService.Create(name, folderId.Value, ContactFormSeeder.SubmissionDocTypeAlias);
        if (content == null)
        {
            _logger.LogError("Failed to create submission content node for '{Name}'", name);
            return;
        }

        content.SetValue("senderName", request.Name);
        content.SetValue("senderEmail", request.Email);
        content.SetValue("subject", request.Subject);
        content.SetValue("message", request.Message);

        _contentService.Save(content);
    }

    private int? GetSubmissionsFolderId()
    {
        var rootContent = _contentService.GetRootContent();
        var folder = rootContent.FirstOrDefault(c => c.Name == ContactFormSeeder.SubmissionsFolderName);
        return folder?.Id;
    }

    private static bool IsValid(ContactFormRequest request, out string error)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            error = "Name and email are required";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

/// <summary>
/// Contact form request model. Works with both JSON and form-encoded POST.
/// </summary>
public class ContactFormRequest
{
    /// <summary>Sender's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sender's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Message subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Message body.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>URL to redirect back to after submission (frontend flow).</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Contact form response model.
/// </summary>
public class ContactFormResponse
{
    /// <summary>Whether the submission was accepted.</summary>
    public bool Success { get; set; }

    /// <summary>Confirmation or error message.</summary>
    public string? Message { get; set; }

    /// <summary>Error details (when failed).</summary>
    public string? Error { get; set; }
}
