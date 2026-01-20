namespace Umbraco.Community.DummyDataSeeder.Seeders;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.DummyDataSeeder.Configuration;
using Umbraco.Community.DummyDataSeeder.Infrastructure;

/// <summary>
/// Seeds content nodes with hierarchical structure.
/// Execution order: 6 (depends on DocumentTypeSeeder, MediaSeeder).
/// </summary>
public class ContentSeeder : BaseSeeder<ContentSeeder>
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly ILocalizationService _localizationService;
    private readonly IDomainService _domainService;

    /// <summary>
    /// Creates a new ContentSeeder instance.
    /// </summary>
    public ContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ILocalizationService localizationService,
        IDomainService domainService,
        ILogger<ContentSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _localizationService = localizationService;
        _domainService = domainService;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 6;

    /// <inheritdoc />
    public override string SeederName => "ContentSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.Content;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var prefix = GetPrefix("content");
        var rootContent = _contentService.GetRootContent();
        return rootContent.Any(c => c.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        // Load document types if not already cached
        LoadDocumentTypesIfNeeded();

        if (Context.SimpleDocTypes.Count == 0)
        {
            Logger.LogWarning("No document types found. Please ensure DocumentTypeSeeder ran first.");
            return Task.CompletedTask;
        }

        // Load languages if not already cached
        LoadLanguagesIfNeeded();
        Logger.LogInformation("Using {Count} languages for content", Context.Languages.Count);

        // Load media items if not already cached
        LoadMediaItemsIfNeeded();

        // Create content tree
        CreateContentTree(cancellationToken);

        Logger.LogInformation("Content seeding completed! Created {Count} content nodes.", Context.CreatedContent.Count);

        return Task.CompletedTask;
    }

    private void LoadDocumentTypesIfNeeded()
    {
        if (Context.SimpleDocTypes.Count > 0) return;

        var allTypes = _contentTypeService.GetAll().ToList();
        var variantPrefix = GetPrefix("variantdoctype");
        var invariantPrefix = GetPrefix("invariantdoctype");

        Context.SimpleDocTypes.AddRange(allTypes.Where(t =>
            t.Alias.StartsWith($"{variantPrefix}Simple", StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith($"{invariantPrefix}Simple", StringComparison.OrdinalIgnoreCase)));

        Context.MediumDocTypes.AddRange(allTypes.Where(t =>
            t.Alias.StartsWith($"{variantPrefix}Medium", StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith($"{invariantPrefix}Medium", StringComparison.OrdinalIgnoreCase)));

        Context.ComplexDocTypes.AddRange(allTypes.Where(t =>
            t.Alias.StartsWith($"{variantPrefix}Complex", StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith($"{invariantPrefix}Complex", StringComparison.OrdinalIgnoreCase)));

        Logger.LogDebug("Loaded doc types - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}",
            Context.SimpleDocTypes.Count, Context.MediumDocTypes.Count, Context.ComplexDocTypes.Count);
    }

    private void LoadLanguagesIfNeeded()
    {
        if (Context.Languages.Count > 0) return;
        Context.Languages = _localizationService.GetAllLanguages().ToList();
    }

    private void LoadMediaItemsIfNeeded()
    {
        if (Context.MediaItems.Count > 0) return;

        // This should be populated by MediaSeeder, but load if not present
        Logger.LogDebug("Media items not pre-loaded, ContentSeeder will operate without media references");
    }

    private void CreateContentTree(CancellationToken cancellationToken)
    {
        var contentConfig = Config.Content;
        var prefix = GetPrefix("content");

        int totalCreated = 0;
        int targetTotal = contentConfig.TotalTarget;

        int simpleTarget = contentConfig.SimpleTarget;
        int mediumTarget = contentConfig.MediumTarget;
        int complexTarget = contentConfig.ComplexTarget;

        int simpleCreated = 0;
        int mediumCreated = 0;
        int complexCreated = 0;

        // Create root sections
        for (int section = 1; section <= contentConfig.RootSections && totalCreated < targetTotal; section++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var sectionDocType = GetRandomDocType("simple");
                var sectionContent = CreateContent($"{prefix}Section_{section}", -1, sectionDocType, "simple");
                Context.CreatedContent.Add(sectionContent);
                simpleCreated++;
                totalCreated++;

                // Each section has categories
                for (int cat = 1; cat <= contentConfig.CategoriesPerSection && totalCreated < targetTotal; cat++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var catDocType = GetRandomDocType("simple");
                    var catContent = CreateContent($"Category_{section}_{cat}", sectionContent.Id, catDocType, "simple");
                    Context.CreatedContent.Add(catContent);
                    simpleCreated++;
                    totalCreated++;

                    // Each category has pages
                    for (int page = 1; page <= contentConfig.PagesPerCategory && totalCreated < targetTotal; page++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Determine complexity based on remaining targets and current distribution
                        var (complexity, docType) = DetermineComplexity(
                            simpleCreated, mediumCreated, complexCreated,
                            simpleTarget, mediumTarget, complexTarget);

                        switch (complexity)
                        {
                            case "simple": simpleCreated++; break;
                            case "medium": mediumCreated++; break;
                            case "complex": complexCreated++; break;
                        }

                        var pageContent = CreateContent($"Page_{section}_{cat}_{page}", catContent.Id, docType, complexity);
                        Context.CreatedContent.Add(pageContent);
                        totalCreated++;

                        // Some pages have detail children (Level 4)
                        if (page % 5 == 0 && totalCreated < targetTotal)
                        {
                            int detailsPerPage = Math.Min(8, targetTotal - totalCreated);
                            for (int detail = 1; detail <= detailsPerPage && totalCreated < targetTotal; detail++)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var detailDocType = GetRandomDocType("simple");
                                var detailContent = CreateContent($"Detail_{section}_{cat}_{page}_{detail}",
                                    pageContent.Id, detailDocType, "simple");
                                Context.CreatedContent.Add(detailContent);
                                simpleCreated++;
                                totalCreated++;
                            }
                        }

                        LogProgress(totalCreated, targetTotal, "content nodes");
                    }
                }

                Logger.LogInformation("Completed root section {Section}/{TotalSections} - Total content created: {Created}/{Target}",
                    section, contentConfig.RootSections, totalCreated, targetTotal);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create content for section {Section}", section);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Final count - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}, Total: {Total}",
            simpleCreated, mediumCreated, complexCreated, totalCreated);
    }

    /// <summary>
    /// Determines which complexity type to use based on configured percentages and current distribution.
    /// Uses weighted random selection to achieve target distribution.
    /// </summary>
    private (string complexity, IContentType docType) DetermineComplexity(
        int simpleCreated, int mediumCreated, int complexCreated,
        int simpleTarget, int mediumTarget, int complexTarget)
    {
        // Calculate remaining needs for each type
        int simpleRemaining = Math.Max(0, simpleTarget - simpleCreated);
        int mediumRemaining = Math.Max(0, mediumTarget - mediumCreated);
        int complexRemaining = Math.Max(0, complexTarget - complexCreated);
        int totalRemaining = simpleRemaining + mediumRemaining + complexRemaining;

        // If all targets met, default to simple
        if (totalRemaining == 0)
        {
            return ("simple", GetRandomDocType("simple"));
        }

        // Weighted random selection based on remaining needs
        int randomValue = Context.Random.Next(totalRemaining);

        if (randomValue < simpleRemaining)
        {
            return ("simple", GetRandomDocType("simple"));
        }
        else if (randomValue < simpleRemaining + mediumRemaining)
        {
            return ("medium", GetRandomDocType("medium"));
        }
        else
        {
            return ("complex", GetRandomDocType("complex"));
        }
    }

    private IContentType GetRandomDocType(string complexity)
    {
        // Use shared Random from context for reproducibility
        return complexity switch
        {
            "simple" => Context.SimpleDocTypes.Count > 0
                ? Context.SimpleDocTypes[Context.Random.Next(Context.SimpleDocTypes.Count)]
                : throw new InvalidOperationException("No simple document types available. Ensure DocumentTypeSeeder ran first."),
            "medium" => Context.MediumDocTypes.Count > 0
                ? Context.MediumDocTypes[Context.Random.Next(Context.MediumDocTypes.Count)]
                : GetRandomDocType("simple"),
            "complex" => Context.ComplexDocTypes.Count > 0
                ? Context.ComplexDocTypes[Context.Random.Next(Context.ComplexDocTypes.Count)]
                : GetRandomDocType("simple"),
            _ => Context.SimpleDocTypes.Count > 0
                ? Context.SimpleDocTypes[Context.Random.Next(Context.SimpleDocTypes.Count)]
                : throw new InvalidOperationException("No simple document types available. Ensure DocumentTypeSeeder ran first.")
        };
    }

    private IContent CreateContent(string name, int parentId, IContentType docType, string complexity)
    {
        var content = _contentService.Create(name, parentId, docType);
        var isVariant = docType.Variations.HasFlag(ContentVariation.Culture);
        var isRootContent = parentId == -1;

        if (isVariant)
        {
            // For variant content, set name and properties for ALL cultures
            foreach (var language in Context.Languages)
            {
                var culture = language.IsoCode;
                content.SetCultureName($"{name} ({culture})", culture);

                SetPropertiesForCulture(content, complexity, culture);
            }

            _contentService.Save(content);

            // Assign domain to root content
            if (isRootContent)
            {
                AssignDomainsToContent(content);
            }
        }
        else
        {
            // For invariant content
            SetPropertiesForCulture(content, complexity, null);
            _contentService.Save(content);
        }

        return content;
    }

    private void SetPropertiesForCulture(IContent content, string complexity, string? culture)
    {
        switch (complexity)
        {
            case "simple":
                SetSimpleProperties(content, culture);
                break;
            case "medium":
                SetMediumProperties(content, culture);
                break;
            case "complex":
                SetComplexProperties(content, culture);
                break;
        }
    }

    private void AssignDomainsToContent(IContent content)
    {
        var existingDomains = _domainService.GetAssignedDomains(content.Id, false).ToList();

        foreach (var language in Context.Languages)
        {
            if (existingDomains.Any(d => d.LanguageIsoCode == language.IsoCode))
                continue;

            var domainName = $"test-{content.Id}-{language.IsoCode.ToLower()}.localhost";

            var domain = new UmbracoDomain(domainName)
            {
                RootContentId = content.Id,
                LanguageIsoCode = language.IsoCode
            };

            _domainService.Save(domain);
        }

        Logger.LogDebug("Assigned {Count} domains to root content '{Name}' (ID: {Id})",
            Context.Languages.Count, content.Name, content.Id);
    }

    private void SetSimpleProperties(IContent content, string? culture)
    {
        if (content.HasProperty("title"))
            content.SetValue("title", Context.Faker.Lorem.Sentence(3), culture);
        if (content.HasProperty("description"))
            content.SetValue("description", Context.Faker.Lorem.Paragraph(), culture);
        if (content.HasProperty("isPublished"))
            content.SetValue("isPublished", Context.Faker.Random.Bool());
    }

    private void SetMediumProperties(IContent content, string? culture)
    {
        if (content.HasProperty("title"))
            content.SetValue("title", Context.Faker.Lorem.Sentence(3), culture);
        if (content.HasProperty("subtitle"))
            content.SetValue("subtitle", Context.Faker.Lorem.Sentence(5), culture);
        if (content.HasProperty("summary"))
            content.SetValue("summary", Context.Faker.Lorem.Paragraph(), culture);

        // Link to random content
        if (content.HasProperty("relatedContent") && Context.CreatedContent.Count > 0)
        {
            var randomContent = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
            content.SetValue("relatedContent", Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString());
        }

        // Add block list content
        if (content.HasProperty("blocks"))
        {
            var blockJson = GenerateSimpleBlockListJson(content, "blocks");
            if (!string.IsNullOrEmpty(blockJson))
                content.SetValue("blocks", blockJson);
        }
    }

    private void SetComplexProperties(IContent content, string? culture)
    {
        if (content.HasProperty("title"))
            content.SetValue("title", Context.Faker.Lorem.Sentence(3), culture);
        if (content.HasProperty("subtitle"))
            content.SetValue("subtitle", Context.Faker.Lorem.Sentence(5), culture);
        if (content.HasProperty("bodyText"))
            content.SetValue("bodyText", Context.Faker.Lorem.Paragraphs(3), culture);

        // Set media picker values
        if (Context.MediaItems.Count > 0)
        {
            if (content.HasProperty("mainImage"))
            {
                var randomMedia = Context.MediaItems[Context.Random.Next(Context.MediaItems.Count)];
                content.SetValue("mainImage", GenerateMediaPickerValue(randomMedia));
            }
            if (content.HasProperty("thumbnailImage"))
            {
                var randomMedia = Context.MediaItems[Context.Random.Next(Context.MediaItems.Count)];
                content.SetValue("thumbnailImage", GenerateMediaPickerValue(randomMedia));
            }
        }

        // Link to content
        if (Context.CreatedContent.Count > 0)
        {
            if (content.HasProperty("primaryContent"))
            {
                var rc = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
                content.SetValue("primaryContent", Udi.Create(Constants.UdiEntityType.Document, rc.Key).ToString());
            }
            if (content.HasProperty("secondaryContent"))
            {
                var rc = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
                content.SetValue("secondaryContent", Udi.Create(Constants.UdiEntityType.Document, rc.Key).ToString());
            }
            if (content.HasProperty("tertiaryContent"))
            {
                var rc = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
                content.SetValue("tertiaryContent", Udi.Create(Constants.UdiEntityType.Document, rc.Key).ToString());
            }
        }

        // Add block lists
        if (content.HasProperty("headerBlocks"))
        {
            var blockJson = GenerateBlockListJsonWithAllComplexities(content, "headerBlocks");
            if (!string.IsNullOrEmpty(blockJson))
                content.SetValue("headerBlocks", blockJson);
        }
        if (content.HasProperty("footerBlocks"))
        {
            var blockJson = GenerateBlockListJsonWithAllComplexities(content, "footerBlocks");
            if (!string.IsNullOrEmpty(blockJson))
                content.SetValue("footerBlocks", blockJson);
        }

        // Add block grid
        if (content.HasProperty("mainGrid"))
        {
            var gridJson = GenerateBlockGridJson(content, "mainGrid");
            if (!string.IsNullOrEmpty(gridJson))
                content.SetValue("mainGrid", gridJson);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string GenerateMediaPickerValue(IMedia media)
    {
        var mediaPickerItems = new[]
        {
            new { key = Guid.NewGuid(), mediaKey = media.Key }
        };
        return JsonSerializer.Serialize(mediaPickerItems, JsonOptions);
    }

    private string? GenerateSimpleBlockListJson(IContent content, string propertyAlias)
    {
        var blockConfig = GetBlockListConfiguration(content, propertyAlias);
        if (blockConfig?.Blocks == null || blockConfig.Blocks.Length == 0) return null;

        var simpleBlock = FindBlockByComplexity(blockConfig.Blocks, "Simple");
        if (simpleBlock == null) return null;

        var elementType = _contentTypeService.Get(simpleBlock.ContentElementTypeKey);
        if (elementType == null) return null;

        var contentUdi = Guid.NewGuid().ToString("N");
        var contentData = BuildElementContentDataObject(elementType, simpleBlock.ContentElementTypeKey, contentUdi);

        var blockListValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = new[] { new { contentUdi = $"umb://element/{contentUdi}" } }
            },
            contentData = new[] { contentData },
            settingsData = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(blockListValue, JsonOptions);
    }

    private string? GenerateBlockListJsonWithAllComplexities(IContent content, string propertyAlias)
    {
        var blockConfig = GetBlockListConfiguration(content, propertyAlias);
        if (blockConfig?.Blocks == null || blockConfig.Blocks.Length == 0) return null;

        var layoutItems = new List<object>();
        var contentDataItems = new List<Dictionary<string, object>>();

        foreach (var complexity in new[] { "Simple", "Medium", "Complex" })
        {
            var block = FindBlockByComplexity(blockConfig.Blocks, complexity);
            if (block == null) continue;

            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType == null) continue;

            var contentUdi = Guid.NewGuid().ToString("N");
            contentDataItems.Add(BuildElementContentDataObject(elementType, block.ContentElementTypeKey, contentUdi));
            layoutItems.Add(new { contentUdi = $"umb://element/{contentUdi}" });
        }

        if (layoutItems.Count == 0) return null;

        var blockListValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            contentData = contentDataItems,
            settingsData = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(blockListValue, JsonOptions);
    }

    private string? GenerateBlockGridJson(IContent content, string propertyAlias)
    {
        var blockConfig = GetBlockGridConfiguration(content, propertyAlias);
        if (blockConfig?.Blocks == null || blockConfig.Blocks.Length == 0) return null;

        var layoutItems = new List<object>();
        var contentDataItems = new List<Dictionary<string, object>>();

        foreach (var complexity in new[] { "Simple", "Medium", "Complex" })
        {
            var block = FindBlockGridByComplexity(blockConfig.Blocks, complexity);
            if (block == null) continue;

            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType == null) continue;

            var contentUdi = Guid.NewGuid().ToString("N");
            contentDataItems.Add(BuildElementContentDataObject(elementType, block.ContentElementTypeKey, contentUdi));
            layoutItems.Add(new
            {
                contentUdi = $"umb://element/{contentUdi}",
                columnSpan = 12,
                rowSpan = 1,
                areas = Array.Empty<object>()
            });
        }

        if (layoutItems.Count == 0) return null;

        var blockGridValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockGrid"] = layoutItems
            },
            contentData = contentDataItems,
            settingsData = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(blockGridValue, JsonOptions);
    }

    private Dictionary<string, object> BuildElementContentDataObject(IContentType elementType, Guid elementTypeKey, string contentUdi)
    {
        var properties = new Dictionary<string, object>
        {
            ["contentTypeKey"] = elementTypeKey.ToString(),
            ["udi"] = $"umb://element/{contentUdi}"
        };

        foreach (var group in elementType.PropertyGroups)
        {
            if (group.PropertyTypes == null) continue;
            foreach (var propType in group.PropertyTypes)
            {
                var propValue = GeneratePropertyValue(propType);
                if (propValue != null)
                    properties[propType.Alias] = propValue;
            }
        }

        return properties;
    }

    private object? GeneratePropertyValue(IPropertyType propType)
    {
        // Use cached data types if available
        if (!Context.DataTypeCache.TryGetValue(propType.DataTypeId, out var dataType))
        {
            dataType = _dataTypeService.GetDataType(propType.DataTypeId);
            if (dataType != null)
                Context.DataTypeCache[propType.DataTypeId] = dataType;
        }

        if (dataType == null) return string.Empty;

        return dataType.EditorAlias switch
        {
            Constants.PropertyEditors.Aliases.TextBox => Context.Faker.Lorem.Sentence(3),
            Constants.PropertyEditors.Aliases.TextArea => Context.Faker.Lorem.Paragraph(),
            Constants.PropertyEditors.Aliases.Boolean => "1",
            Constants.PropertyEditors.Aliases.Label => string.Empty,
            Constants.PropertyEditors.Aliases.Integer => Context.Faker.Random.Int(1, 100),
            _ => string.Empty
        };
    }

    private BlockListConfiguration? GetBlockListConfiguration(IContent content, string propertyAlias)
    {
        var contentType = _contentTypeService.Get(content.ContentTypeId);
        if (contentType == null) return null;

        var propertyType = FindPropertyType(contentType, propertyAlias);
        if (propertyType == null) return null;

        var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
        return dataType?.Configuration as BlockListConfiguration;
    }

    private BlockGridConfiguration? GetBlockGridConfiguration(IContent content, string propertyAlias)
    {
        var contentType = _contentTypeService.Get(content.ContentTypeId);
        if (contentType == null) return null;

        var propertyType = FindPropertyType(contentType, propertyAlias);
        if (propertyType == null) return null;

        var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
        return dataType?.Configuration as BlockGridConfiguration;
    }

    private static IPropertyType? FindPropertyType(IContentType contentType, string alias)
    {
        foreach (var group in contentType.PropertyGroups)
        {
            var pt = group.PropertyTypes?.FirstOrDefault(p => p.Alias == alias);
            if (pt != null) return pt;
        }
        return contentType.PropertyTypes.FirstOrDefault(p => p.Alias == alias);
    }

    private BlockListConfiguration.BlockConfiguration? FindBlockByComplexity(
        BlockListConfiguration.BlockConfiguration[] blocks, string complexity)
    {
        foreach (var block in blocks)
        {
            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType?.Alias.Contains(complexity, StringComparison.OrdinalIgnoreCase) == true)
                return block;
        }
        return null;
    }

    private BlockGridConfiguration.BlockGridBlockConfiguration? FindBlockGridByComplexity(
        BlockGridConfiguration.BlockGridBlockConfiguration[] blocks, string complexity)
    {
        foreach (var block in blocks)
        {
            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType?.Alias.Contains(complexity, StringComparison.OrdinalIgnoreCase) == true)
                return block;
        }
        return null;
    }
}
