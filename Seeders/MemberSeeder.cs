namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Seeds test members (front-end users) with passwords, member group assignments,
/// and frontend login/member area pages.
/// Execution order: 8 (after UserSeeder).
/// </summary>
public class MemberSeeder : BaseSeeder<MemberSeeder>
{
    private readonly IMemberService _memberService;
    private readonly IMemberGroupService _memberGroupService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly IContentTypeService _contentTypeService;
    private readonly ITemplateService _templateService;
    private readonly IContentService _contentService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IPasswordHasher<MemberIdentityUser> _passwordHasher;

    /// <summary>
    /// Creates a new MemberSeeder instance.
    /// </summary>
    public MemberSeeder(
        IMemberService memberService,
        IMemberGroupService memberGroupService,
        IMemberTypeService memberTypeService,
        IContentTypeService contentTypeService,
        ITemplateService templateService,
        IContentService contentService,
        IShortStringHelper shortStringHelper,
        IPasswordHasher<MemberIdentityUser> passwordHasher,
        IScopeProvider scopeProvider,
        ILogger<MemberSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
    {
        _memberService = memberService;
        _memberGroupService = memberGroupService;
        _memberTypeService = memberTypeService;
        _contentTypeService = contentTypeService;
        _templateService = templateService;
        _contentService = contentService;
        _shortStringHelper = shortStringHelper;
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 8;

    /// <inheritdoc />
    public override string SeederName => "MemberSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.Members;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var prefix = GetPrefix(PrefixType.Member);
        var members = _memberService.GetAll(0, 100, out _);
        return members.Any(m => m.Username.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the login doc type alias based on the configured prefix.
    /// </summary>
    private string GetLoginDocTypeAlias() => $"{GetPrefix(PrefixType.Member)}Login";

    /// <summary>
    /// Gets the member area doc type alias based on the configured prefix.
    /// </summary>
    private string GetMemberAreaDocTypeAlias() => $"{GetPrefix(PrefixType.Member)}Area";

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var targetCount = Config.Members.Count;
        var prefix = GetPrefix(PrefixType.Member);
        var password = Config.Members.DefaultPassword;

        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create {Count} members with prefix '{Prefix}'", targetCount, prefix);
            Logger.LogInformation("[DRY-RUN] Would create member login and member area pages");
            return;
        }

        // Verify the default "Member" member type exists
        var memberType = _memberTypeService.Get("Member");
        if (memberType == null)
        {
            Logger.LogError("Default 'Member' member type not found. Cannot seed members");
            if (Options.StopOnError) throw new InvalidOperationException("Default 'Member' member type not found");
            return;
        }

        // Phase 1: Create member groups
        var groupNames = new[] { $"{prefix}Standard", $"{prefix}Premium", $"{prefix}VIP" };
        foreach (var groupName in groupNames)
        {
            if (_memberGroupService.GetByName(groupName) == null)
            {
                var group = new MemberGroup { Name = groupName };
                await _memberGroupService.CreateAsync(group);
                Logger.LogDebug("Created member group '{GroupName}'", groupName);
            }
        }

        // Phase 2: Create frontend pages (document types, templates, content)
        // Frontend pages are real content, so skip in dry-run mode
        if (!IsDryRun)
        {
            await CreateFrontendPages();
        }

        // Phase 3: Seed members
        await SeedMembers(targetCount, prefix, password, groupNames, cancellationToken);
    }

    private async Task SeedMembers(int targetCount, string prefix, string password, string[] groupNames, CancellationToken cancellationToken)
    {
        // Pre-load existing member usernames using pagination
        var existingUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int pageSize = 1000;
        long totalMembers;
        int pageIndex = 0;

        do
        {
            var membersPage = _memberService.GetAll(pageIndex, pageSize, out totalMembers);
            foreach (var member in membersPage)
            {
                existingUsernames.Add(member.Username);
            }
            pageIndex++;
        } while (pageIndex * pageSize < totalMembers);

        Logger.LogDebug("Found {Count} existing members", existingUsernames.Count);

        // Distribution based on configured percentages
        int standardThreshold = targetCount * Config.Members.StandardPercent / 100;
        int premiumThreshold = targetCount * (Config.Members.StandardPercent + Config.Members.PremiumPercent) / 100;

        int created = 0;
        int batchCount = 0;
        IScope? currentScope = null;

        try
        {
            for (int i = 1; i <= targetCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var username = $"{prefix}{i}";

                if (existingUsernames.Contains(username))
                {
                    Logger.LogDebug("Member {Username} already exists, skipping", username);
                    continue;
                }

                try
                {
                    if (currentScope == null)
                    {
                        currentScope = CreateScopedBatch();
                        batchCount = 0;
                    }

                    var firstName = Context.Faker.Name.FirstName();
                    var lastName = Context.Faker.Name.LastName();
                    var email = $"{username.ToLowerInvariant()}@example.com";

                    // CreateMember builds the object in memory without persisting
                    var member = _memberService.CreateMember(username, email, $"{firstName} {lastName}", "Member");
                    member.IsApproved = true;
                    // Hash password using Identity's password hasher so MemberSignInManager can verify it
                    // A dummy user is passed because the hasher interface requires a non-null user parameter
                    member.RawPasswordValue = _passwordHasher.HashPassword(new MemberIdentityUser(), password);
                    _memberService.Save(member);

                    // Assign to group based on distribution
                    string assignedGroup;
                    if (i <= standardThreshold)
                    {
                        assignedGroup = groupNames[0]; // Standard
                    }
                    else if (i <= premiumThreshold)
                    {
                        assignedGroup = groupNames[1]; // Premium
                    }
                    else
                    {
                        assignedGroup = groupNames[2]; // VIP
                    }

                    _memberService.AssignRoles(new[] { member.Id }, new[] { assignedGroup });

                    created++;
                    batchCount++;

                    if (batchCount >= Options.BatchSize)
                    {
                        currentScope.Complete();
                        currentScope.Dispose();
                        currentScope = null;
                    }

                    LogProgress(created, targetCount, "members");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to create member {Username}", username);
                    if (Options.StopOnError) throw;
                }
            }

            if (currentScope != null)
            {
                currentScope.Complete();
            }
        }
        finally
        {
            currentScope?.Dispose();
        }

        Logger.LogInformation("Seeded {Created} test members (target: {Target})", created, targetCount);
    }

    #region Frontend Pages

    private async Task CreateFrontendPages()
    {
        var loginAlias = GetLoginDocTypeAlias();
        var memberAreaAlias = GetMemberAreaDocTypeAlias();
        var prefix = GetPrefix(PrefixType.Member);

        // Check if pages already exist by doc type alias
        var existingTypes = _contentTypeService.GetAll();
        if (existingTypes.Any(t => t.Alias == loginAlias))
        {
            Logger.LogDebug("Member frontend pages already exist, skipping");
            return;
        }

        Logger.LogInformation("Creating member login and member area pages...");

        try
        {
            // Create templates with dynamic aliases
            var loginTemplate = await CreateLoginTemplate(loginAlias, memberAreaAlias);
            var memberAreaTemplate = await CreateMemberAreaTemplate(memberAreaAlias, loginAlias);

            // Create document types
            var loginDocType = await CreateDocType(loginAlias, $"{prefix}Login", "icon-key", loginTemplate);
            var memberAreaDocType = await CreateDocType(memberAreaAlias, $"{prefix}Area", "icon-user", memberAreaTemplate);

            // Create and publish content nodes at root
            CreateAndPublishContentNode($"{prefix}Member Login", loginDocType);
            CreateAndPublishContentNode($"{prefix}Member Area", memberAreaDocType);

            Logger.LogInformation("Created member login and member area pages");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to create member frontend pages");
            if (Options.StopOnError) throw;
        }
    }

    private async Task<ITemplate> CreateLoginTemplate(string loginAlias, string memberAreaAlias)
    {
        var template = new Template(_shortStringHelper, $"{GetPrefix(PrefixType.Member)}Login", loginAlias)
        {
            Content = $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{{
    Layout = null;
    var error = TempData[""LoginError""] as string;
    var memberAreaUrl = Umbraco.ContentAtRoot()
        .FirstOrDefault(x => x.ContentType.Alias == ""{memberAreaAlias}"")?.Url() ?? ""/member-area/"";
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Member Login</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px auto; max-width: 400px; }}
        form {{ display: flex; flex-direction: column; gap: 12px; }}
        input {{ padding: 10px; font-size: 16px; border: 1px solid #ccc; border-radius: 4px; }}
        button {{ padding: 12px; font-size: 16px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }}
        button:hover {{ background: #0056b3; }}
        .error {{ color: red; margin-bottom: 10px; }}
        .info {{ color: #666; font-size: 14px; margin-top: 20px; }}
    </style>
</head>
<body>
    <h1>Member Login</h1>
    @if (!string.IsNullOrEmpty(error))
    {{
        <div class=""error"">@error</div>
    }}
    <form method=""post"" action=""/umbraco/api/memberlogin/login"">
        <input type=""hidden"" name=""RedirectUrl"" value=""@memberAreaUrl"" />
        <input type=""hidden"" name=""LoginUrl"" value=""@Model.Url()"" />
        <label for=""username"">Username</label>
        <input type=""text"" id=""username"" name=""Username"" required />
        <label for=""password"">Password</label>
        <input type=""password"" id=""password"" name=""Password"" required />
        <button type=""submit"">Login</button>
    </form>
    <div class=""info"">
        <p>Generated by PerformanceTestDataSeeder</p>
    </div>
</body>
</html>"
        };

        var result = await _templateService.CreateAsync(template, Constants.Security.SuperUserKey);
        return result.Success ? result.Result : template;
    }

    private async Task<ITemplate> CreateMemberAreaTemplate(string memberAreaAlias, string loginAlias)
    {
        var template = new Template(_shortStringHelper, $"{GetPrefix(PrefixType.Member)}Area", memberAreaAlias)
        {
            Content = $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Web.Common.Security
@inject MemberManager Members
@{{
    Layout = null;
    var isLoggedIn = Members.IsLoggedIn();
    var member = isLoggedIn ? await Members.GetCurrentMemberAsync() : null;
    var loginUrl = Umbraco.ContentAtRoot()
        .FirstOrDefault(x => x.ContentType.Alias == ""{loginAlias}"")?.Url() ?? ""/member-login/"";
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Member Area</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px auto; max-width: 600px; }}
        .member-info {{ background: #f0f8f0; padding: 20px; border-radius: 8px; border: 1px solid #28a745; }}
        .member-info h2 {{ color: #28a745; margin-top: 0; }}
        .member-info p {{ margin: 8px 0; }}
        .label {{ font-weight: bold; }}
        .not-logged-in {{ background: #fff3cd; padding: 20px; border-radius: 8px; border: 1px solid #ffc107; }}
        a.login-link {{ display: inline-block; padding: 10px 20px; background: #007bff; color: white; text-decoration: none; border-radius: 4px; }}
        .info {{ color: #666; font-size: 14px; margin-top: 20px; }}
    </style>
</head>
<body>
    <h1>Member Area</h1>
    @if (isLoggedIn && member != null)
    {{
        <div class=""member-info"">
            <h2>Welcome, @member.Name!</h2>
            <p><span class=""label"">Username:</span> @member.UserName</p>
            <p><span class=""label"">Email:</span> @member.Email</p>
            <p><span class=""label"">Status:</span> Authenticated</p>
        </div>
        <form method=""post"" action=""/umbraco/api/memberlogin/logout"" style=""margin-top: 15px;"">
            <input type=""hidden"" name=""RedirectUrl"" value=""@loginUrl"" />
            <button type=""submit"" style=""padding: 10px 20px; background: #dc3545; color: white; border: none; border-radius: 4px; cursor: pointer;"">Logout</button>
        </form>
    }}
    else
    {{
        <div class=""not-logged-in"">
            <h2>Not logged in</h2>
            <p>You must be logged in to view this page.</p>
            <a href=""@loginUrl"" class=""login-link"">Go to Login</a>
        </div>
    }}
    <div class=""info"">
        <p>Generated by PerformanceTestDataSeeder</p>
    </div>
</body>
</html>"
        };

        var result = await _templateService.CreateAsync(template, Constants.Security.SuperUserKey);
        return result.Success ? result.Result : template;
    }

    private async Task<IContentType> CreateDocType(string alias, string name, string icon, ITemplate template)
    {
        var docType = new ContentType(_shortStringHelper, Context.TestPagesFolderId)
        {
            Alias = alias,
            Name = name,
            Icon = icon,
            AllowedAsRoot = true,
            Variations = ContentVariation.Nothing
        };

        docType.AllowedTemplates = new[] { template };
        docType.SetDefaultTemplate(template);

        await _contentTypeService.CreateAsync(docType, Constants.Security.SuperUserKey);
        return docType;
    }

    private void CreateAndPublishContentNode(string name, IContentType docType)
    {
        var content = _contentService.Create(name, Constants.System.Root, docType.Alias);
        if (content == null)
        {
            Logger.LogError("Failed to create content node '{Name}' with doc type '{Alias}'", name, docType.Alias);
            return;
        }

        _contentService.Save(content);
        _contentService.Publish(content, Array.Empty<string>());
        Logger.LogDebug("Created and published content node '{Name}'", name);
    }

    #endregion
}
