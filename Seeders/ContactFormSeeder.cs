namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Seeds a contact form page and a submission doc type for persisting form data.
/// Execution order: 9 (after MemberSeeder).
/// </summary>
public class ContactFormSeeder : BaseSeeder<ContactFormSeeder>
{
    private readonly IContentTypeService _contentTypeService;
    private readonly ITemplateService _templateService;
    private readonly IContentService _contentService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IShortStringHelper _shortStringHelper;

    /// <summary>Doc type alias for the contact form page.</summary>
    internal const string FormDocTypeAlias = "testContactForm";

    /// <summary>Doc type alias for contact form submissions stored as content.</summary>
    internal const string SubmissionDocTypeAlias = "testContactSubmission";

    /// <summary>Content name for the submissions folder.</summary>
    internal const string SubmissionsFolderName = "Contact Submissions";

    /// <summary>
    /// Creates a new ContactFormSeeder instance.
    /// </summary>
    public ContactFormSeeder(
        IContentTypeService contentTypeService,
        ITemplateService templateService,
        IContentService contentService,
        IDataTypeService dataTypeService,
        IShortStringHelper shortStringHelper,
        IScopeProvider scopeProvider,
        ILogger<ContactFormSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
    {
        _contentTypeService = contentTypeService;
        _templateService = templateService;
        _contentService = contentService;
        _dataTypeService = dataTypeService;
        _shortStringHelper = shortStringHelper;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 9;

    /// <inheritdoc />
    public override string SeederName => "ContactFormSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.ContactForm;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var existingTypes = _contentTypeService.GetAll();
        return existingTypes.Any(t => t.Alias == FormDocTypeAlias);
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create contact form page and submission doc type");
            return;
        }

        // Load built-in data types for submission properties
        var allDataTypes = (await _dataTypeService.GetAllAsync(Array.Empty<Guid>())).ToList();
        var textstringDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.Textbox);
        var textareaDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.Textarea);

        if (textstringDataType == null || textareaDataType == null)
        {
            Logger.LogError("Required data types (Textstring/Textarea) not found. Cannot create contact form");
            if (Options.StopOnError) throw new InvalidOperationException("Required data types not found");
            return;
        }

        Logger.LogInformation("Creating contact form page and submission doc type...");

        // 1. Create submission doc type (no template — not a renderable page)
        await CreateSubmissionDocType(textstringDataType, textareaDataType);

        // 2. Create submissions folder at root
        await CreateSubmissionsFolder();

        // 3. Create contact form page with template
        await CreateContactFormPage();

        Logger.LogInformation("Created contact form page and submission doc type");
    }

    private async Task CreateSubmissionDocType(IDataType textstringDataType, IDataType textareaDataType)
    {
        var docType = new ContentType(_shortStringHelper, Context.TestPagesFolderId)
        {
            Alias = SubmissionDocTypeAlias,
            Name = "Contact Submission",
            Icon = "icon-mailbox",
            AllowedAsRoot = false,
            Variations = ContentVariation.Nothing
        };

        var group = new PropertyGroup(true) { Alias = "submission", Name = "Submission", SortOrder = 1 };

        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, textstringDataType)
        {
            Alias = "senderName",
            Name = "Sender Name",
            SortOrder = 1
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, textstringDataType)
        {
            Alias = "senderEmail",
            Name = "Sender Email",
            SortOrder = 2
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, textstringDataType)
        {
            Alias = "subject",
            Name = "Subject",
            SortOrder = 3
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, textareaDataType)
        {
            Alias = "message",
            Name = "Message",
            SortOrder = 4
        });

        docType.PropertyGroups.Add(group);

        await _contentTypeService.CreateAsync(docType, Constants.Security.SuperUserKey);
        Logger.LogDebug("Created submission doc type '{Alias}'", SubmissionDocTypeAlias);
    }

    private async Task CreateSubmissionsFolder()
    {
        // Check if folder already exists
        var rootContent = _contentService.GetRootContent();
        if (rootContent.Any(c => c.Name == SubmissionsFolderName))
        {
            Logger.LogDebug("Submissions folder already exists");
            return;
        }

        // Use the submission doc type as a container — allow it at root for the folder
        var submissionDocType = _contentTypeService.Get(SubmissionDocTypeAlias);
        if (submissionDocType != null)
        {
            submissionDocType.AllowedAsRoot = true;

            // Allow submission doc type as child of itself (folder contains submissions)
            submissionDocType.AllowedContentTypes = new[]
            {
                new ContentTypeSort(submissionDocType.Key, 0, submissionDocType.Alias)
            };

            await _contentTypeService.UpdateAsync(submissionDocType, Constants.Security.SuperUserKey);
        }

        var folder = _contentService.Create(SubmissionsFolderName, Constants.System.Root, SubmissionDocTypeAlias);
        if (folder == null)
        {
            Logger.LogError("Failed to create submissions folder");
            return;
        }

        _contentService.Save(folder);
        _contentService.Publish(folder, Array.Empty<string>());
        Logger.LogDebug("Created submissions folder");
    }

    private async Task CreateContactFormPage()
    {
        var template = new Template(_shortStringHelper, "Contact Form", FormDocTypeAlias)
        {
            Content = @"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{
    Layout = null;
    var success = TempData[""ContactFormSuccess""] as string;
    var error = TempData[""ContactFormError""] as string;
}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Contact Us</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px auto; max-width: 500px; }
        form { display: flex; flex-direction: column; gap: 12px; }
        input, textarea { padding: 10px; font-size: 16px; border: 1px solid #ccc; border-radius: 4px; }
        textarea { min-height: 120px; resize: vertical; }
        button { padding: 12px; font-size: 16px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }
        button:hover { background: #0056b3; }
        .success { color: #28a745; background: #f0f8f0; padding: 12px; border-radius: 4px; border: 1px solid #28a745; margin-bottom: 10px; }
        .error { color: red; margin-bottom: 10px; }
        .info { color: #666; font-size: 14px; margin-top: 20px; }
    </style>
</head>
<body>
    <h1>Contact Us</h1>
    @if (!string.IsNullOrEmpty(success))
    {
        <div class=""success"">@success</div>
    }
    @if (!string.IsNullOrEmpty(error))
    {
        <div class=""error"">@error</div>
    }
    <form method=""post"" action=""/umbraco/api/contactform/form-submit"">
        <input type=""hidden"" name=""ReturnUrl"" value=""@Model.Url()"" />
        <label for=""name"">Name</label>
        <input type=""text"" id=""name"" name=""Name"" required />
        <label for=""email"">Email</label>
        <input type=""email"" id=""email"" name=""Email"" required />
        <label for=""subject"">Subject</label>
        <input type=""text"" id=""subject"" name=""Subject"" />
        <label for=""message"">Message</label>
        <textarea id=""message"" name=""Message"" required></textarea>
        <button type=""submit"">Send Message</button>
    </form>
    <div class=""info"">
        <p>Generated by PerformanceTestDataSeeder</p>
    </div>
</body>
</html>"
        };

        var templateResult = await _templateService.CreateAsync(template, Constants.Security.SuperUserKey);
        var createdTemplate = templateResult.Success ? templateResult.Result : template;

        var docType = new ContentType(_shortStringHelper, Context.TestPagesFolderId)
        {
            Alias = FormDocTypeAlias,
            Name = "Contact Form",
            Icon = "icon-message",
            AllowedAsRoot = true,
            Variations = ContentVariation.Nothing
        };

        docType.AllowedTemplates = new[] { createdTemplate };
        docType.SetDefaultTemplate(createdTemplate);
        await _contentTypeService.CreateAsync(docType, Constants.Security.SuperUserKey);

        var content = _contentService.Create("Contact Us", Constants.System.Root, FormDocTypeAlias);
        if (content == null)
        {
            Logger.LogError("Failed to create contact form content node");
            return;
        }

        _contentService.Save(content);
        _contentService.Publish(content, Array.Empty<string>());
    }
}
