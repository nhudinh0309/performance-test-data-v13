namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Security;

/// <summary>
/// Form-based controller for member login/logout from frontend pages.
/// Accepts form POST, authenticates, and redirects.
/// POST /umbraco/api/memberlogin/login
/// POST /umbraco/api/memberlogin/logout
/// </summary>
[Route("umbraco/api/memberlogin")]
public class MemberLoginController : Controller
{
    private readonly MemberSignInManager _memberSignInManager;

    /// <summary>
    /// Creates a new MemberLoginController instance.
    /// </summary>
    public MemberLoginController(MemberSignInManager memberSignInManager)
    {
        _memberSignInManager = memberSignInManager;
    }

    /// <summary>
    /// Authenticates a member via form POST and redirects.
    /// On success, redirects to RedirectUrl (member area page).
    /// On failure, redirects to LoginUrl (login page) with error in TempData.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] MemberLoginFormRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            TempData["LoginError"] = "Username and password are required";
            return LocalRedirect(request.LoginUrl ?? "/");
        }

        var result = await _memberSignInManager.PasswordSignInAsync(
            request.Username, request.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return LocalRedirect(request.RedirectUrl ?? "/");
        }

        TempData["LoginError"] = MemberAuthHelper.GetLoginError(result);
        return LocalRedirect(request.LoginUrl ?? "/");
    }

    /// <summary>
    /// Signs out the current member via form POST and redirects.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromForm] MemberLogoutFormRequest request)
    {
        await _memberSignInManager.SignOutAsync();
        return LocalRedirect(request.RedirectUrl ?? "/");
    }
}

/// <summary>
/// Form request model for member login.
/// </summary>
public class MemberLoginFormRequest
{
    /// <summary>Member username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Member password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>URL to redirect to after successful login.</summary>
    public string? RedirectUrl { get; set; }

    /// <summary>URL to redirect back to on login failure.</summary>
    public string? LoginUrl { get; set; }
}

/// <summary>
/// Form request model for member logout.
/// </summary>
public class MemberLogoutFormRequest
{
    /// <summary>URL to redirect to after logout.</summary>
    public string? RedirectUrl { get; set; }
}
