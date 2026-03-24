namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;

/// <summary>
/// JSON API controller for member authentication (k6 / load test scripts).
/// POST /umbraco/api/memberauth/login
/// POST /umbraco/api/memberauth/logout
/// GET  /umbraco/api/memberauth/me
/// </summary>
[ApiController]
[Route("umbraco/api/memberauth")]
public class MemberAuthController : Controller
{
    private readonly MemberManager _memberManager;
    private readonly MemberSignInManager _memberSignInManager;

    /// <summary>
    /// Creates a new MemberAuthController instance.
    /// </summary>
    public MemberAuthController(
        MemberManager memberManager,
        MemberSignInManager memberSignInManager)
    {
        _memberManager = memberManager;
        _memberSignInManager = memberSignInManager;
    }

    /// <summary>
    /// Authenticates a member and issues an auth cookie. Returns JSON.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] MemberAuthLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new MemberAuthResponse { Success = false, Error = "Username and password are required" });
        }

        var result = await _memberSignInManager.PasswordSignInAsync(
            request.Username, request.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var member = await _memberManager.FindByNameAsync(request.Username);
            return Ok(new MemberAuthResponse
            {
                Success = true,
                Username = request.Username,
                Name = member?.Name,
                Email = member?.Email
            });
        }

        return Unauthorized(new MemberAuthResponse { Success = false, Error = MemberAuthHelper.GetLoginError(result) });
    }

    /// <summary>
    /// Signs out the current member. Returns JSON.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _memberSignInManager.SignOutAsync();
        return Ok(new MemberAuthResponse { Success = true });
    }

    /// <summary>
    /// Returns the current authentication state as JSON.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new MemberAuthResponse { Success = false, Error = "Not authenticated" });
        }

        var member = await _memberManager.GetCurrentMemberAsync();
        if (member == null)
        {
            return Ok(new MemberAuthResponse { Success = false, Error = "Not authenticated" });
        }

        return Ok(new MemberAuthResponse
        {
            Success = true,
            Username = member.UserName,
            Name = member.Name,
            Email = member.Email
        });
    }
}

/// <summary>
/// JSON request model for member login.
/// </summary>
public class MemberAuthLoginRequest
{
    /// <summary>Member username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Member password.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// JSON response model for member authentication endpoints.
/// </summary>
public class MemberAuthResponse
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Member username (when authenticated).</summary>
    public string? Username { get; set; }

    /// <summary>Member display name (when authenticated).</summary>
    public string? Name { get; set; }

    /// <summary>Member email (when authenticated).</summary>
    public string? Email { get; set; }

    /// <summary>Error message (when failed).</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Shared helper for login error messages.
/// </summary>
internal static class MemberAuthHelper
{
    internal static string GetLoginError(Microsoft.AspNetCore.Identity.SignInResult result) =>
        result.IsLockedOut ? "Account is locked out"
        : result.IsNotAllowed ? "Login not allowed (account may not be approved)"
        : "Invalid username or password";
}
