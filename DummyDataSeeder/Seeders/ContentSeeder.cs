using Bogus;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

public class ContentSeeder : IHostedService
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IMediaService _mediaService;
    private readonly ILocalizationService _localizationService;
    private readonly IDomainService _domainService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    private readonly Faker _faker = new("en");

    // Cache document types
    private readonly List<IContentType> _simpleDocTypes = new();
    private readonly List<IContentType> _mediumDocTypes = new();
    private readonly List<IContentType> _complexDocTypes = new();

    // Cache created content for linking
    private readonly List<IContent> _createdContent = new();

    // Cache media for complex content
    private readonly List<IMedia> _mediaItems = new();

    private List<ILanguage> _allLanguages = new();

    public ContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IMediaService mediaService,
        ILocalizationService localizationService,
        IDomainService domainService,
        IShortStringHelper shortStringHelper,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _mediaService = mediaService;
        _localizationService = localizationService;
        _domainService = domainService;
        _shortStringHelper = shortStringHelper;
        _runtimeState = runtimeState;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed when Umbraco is fully installed and running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            Console.WriteLine("ContentSeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

        SeedContent();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedContent()
    {
        // Check if already seeded
        var rootContent = _contentService.GetRootContent();
        if (rootContent.Any(c => c.Name?.StartsWith("Test_") == true)) return;

        Console.WriteLine("Starting content seeding...");

        // Load document types
        LoadDocumentTypes();
        if (_simpleDocTypes.Count == 0)
        {
            Console.WriteLine("No document types found. Please run DocumentTypeSeeder first.");
            return;
        }

        // Load all languages
        _allLanguages = _localizationService.GetAllLanguages().ToList();
        Console.WriteLine($"Loaded {_allLanguages.Count} languages for content publishing.");

        // Load some media items for complex content
        LoadMediaItems();

        // Create content structure: 3-4 levels
        // Level 1: Root sections (~50 nodes)
        // Level 2: Categories (~500 nodes)
        // Level 3: Pages (~5000 nodes)
        // Level 4: Detail pages (~19450 nodes)
        // Total: ~25000

        Console.WriteLine("Creating root sections...");
        CreateContentTree();

        Console.WriteLine($"Content seeding completed! Created {_createdContent.Count} content nodes.");
    }

    private void LoadDocumentTypes()
    {
        var allTypes = _contentTypeService.GetAll().ToList();

        // Simple types (Variant + Invariant)
        _simpleDocTypes.AddRange(allTypes.Where(t =>
            t.Alias.StartsWith("testVariantSimple") || t.Alias.StartsWith("testInvariantSimple")));

        // Medium types
        _mediumDocTypes.AddRange(allTypes.Where(t =>
            t.Alias.StartsWith("testVariantMedium") || t.Alias.StartsWith("testInvariantMedium")));

        // Complex types
        _complexDocTypes.AddRange(allTypes.Where(t =>
            t.Alias.StartsWith("testVariantComplex") || t.Alias.StartsWith("testInvariantComplex")));

        Console.WriteLine($"Loaded {_simpleDocTypes.Count} simple, {_mediumDocTypes.Count} medium, {_complexDocTypes.Count} complex document types.");
    }

    private void LoadMediaItems()
    {
        // Get some media items for use in complex content
        var allMedia = _mediaService.GetRootMedia()
            .SelectMany(m => _mediaService.GetPagedDescendants(m.Id, 0, 100, out _))
            .Where(m => m.ContentType.Alias == Constants.Conventions.MediaTypes.Image)
            .Take(100)
            .ToList();

        _mediaItems.AddRange(allMedia);
        Console.WriteLine($"Loaded {_mediaItems.Count} media items for content linking.");
    }

    private void CreateContentTree()
    {
        var contentConfig = _config.Content;

        int totalCreated = 0;
        int targetTotal = contentConfig.TotalTarget;

        // Distribution based on config percentages
        int simpleTarget = contentConfig.SimpleTarget;
        int mediumTarget = contentConfig.MediumTarget;
        int complexTarget = contentConfig.ComplexTarget;

        int simpleCreated = 0;
        int mediumCreated = 0;
        int complexCreated = 0;

        // Create root sections
        for (int section = 1; section <= contentConfig.RootSections && totalCreated < targetTotal; section++)
        {
            var sectionDocType = GetRandomDocType("simple");
            var sectionContent = CreateContent($"Test_Section_{section}", -1, sectionDocType, "simple");
            _createdContent.Add(sectionContent);
            simpleCreated++;
            totalCreated++;

            // Each section has categories
            for (int cat = 1; cat <= contentConfig.CategoriesPerSection && totalCreated < targetTotal; cat++)
            {
                var catDocType = GetRandomDocType("simple");
                var catContent = CreateContent($"Category_{section}_{cat}", sectionContent.Id, catDocType, "simple");
                _createdContent.Add(catContent);
                simpleCreated++;
                totalCreated++;

                // Each category has pages
                for (int page = 1; page <= contentConfig.PagesPerCategory && totalCreated < targetTotal; page++)
                {
                    string complexity;
                    IContentType docType;

                    // Distribute content types
                    if (simpleCreated < simpleTarget && (mediumCreated >= mediumTarget || page % 3 == 0))
                    {
                        complexity = "simple";
                        docType = GetRandomDocType("simple");
                        simpleCreated++;
                    }
                    else if (mediumCreated < mediumTarget && (complexCreated >= complexTarget || page % 3 == 1))
                    {
                        complexity = "medium";
                        docType = GetRandomDocType("medium");
                        mediumCreated++;
                    }
                    else if (complexCreated < complexTarget)
                    {
                        complexity = "complex";
                        docType = GetRandomDocType("complex");
                        complexCreated++;
                    }
                    else
                    {
                        complexity = "simple";
                        docType = GetRandomDocType("simple");
                        simpleCreated++;
                    }

                    var pageContent = CreateContent($"Page_{section}_{cat}_{page}", catContent.Id, docType, complexity);
                    _createdContent.Add(pageContent);
                    totalCreated++;

                    // Some pages have detail children (Level 4)
                    if (page % 5 == 0 && totalCreated < targetTotal)
                    {
                        int detailsPerPage = Math.Min(8, targetTotal - totalCreated);
                        for (int detail = 1; detail <= detailsPerPage && totalCreated < targetTotal; detail++)
                        {
                            var detailDocType = GetRandomDocType("simple");
                            var detailContent = CreateContent($"Detail_{section}_{cat}_{page}_{detail}",
                                pageContent.Id, detailDocType, "simple");
                            _createdContent.Add(detailContent);
                            simpleCreated++;
                            totalCreated++;
                        }
                    }

                    if (totalCreated % 500 == 0)
                    {
                        Console.WriteLine($"Created {totalCreated}/{targetTotal} content nodes (Simple: {simpleCreated}, Medium: {mediumCreated}, Complex: {complexCreated})...");
                    }
                }
            }
        }

        Console.WriteLine($"Final count - Simple: {simpleCreated}, Medium: {mediumCreated}, Complex: {complexCreated}, Total: {totalCreated}");
    }

    private IContentType GetRandomDocType(string complexity)
    {
        var random = new Random();
        return complexity switch
        {
            "simple" => _simpleDocTypes[random.Next(_simpleDocTypes.Count)],
            "medium" => _mediumDocTypes.Count > 0 ? _mediumDocTypes[random.Next(_mediumDocTypes.Count)] : _simpleDocTypes[random.Next(_simpleDocTypes.Count)],
            "complex" => _complexDocTypes.Count > 0 ? _complexDocTypes[random.Next(_complexDocTypes.Count)] : _simpleDocTypes[random.Next(_simpleDocTypes.Count)],
            _ => _simpleDocTypes[random.Next(_simpleDocTypes.Count)]
        };
    }

    private IContent CreateContent(string name, int parentId, IContentType docType, string complexity)
    {
        var content = _contentService.Create(name, parentId, docType);
        var isVariant = docType.Variations.HasFlag(ContentVariation.Culture);
        var isRootContent = parentId == -1;

        if (isVariant)
        {
            // For variant content types, set name and properties for ALL cultures
            foreach (var language in _allLanguages)
            {
                var culture = language.IsoCode;
                content.SetCultureName($"{name} ({culture})", culture);

                // Set properties for this culture
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

            // Save content (without publishing)
            _contentService.Save(content);

            // Assign domain (hostname + culture) to root content for each language
            if (isRootContent)
            {
                AssignDomainsToContent(content);
            }
        }
        else
        {
            // For invariant content types
            switch (complexity)
            {
                case "simple":
                    SetSimpleProperties(content, null);
                    break;
                case "medium":
                    SetMediumProperties(content, null);
                    break;
                case "complex":
                    SetComplexProperties(content, null);
                    break;
            }

            // Save content (without publishing)
            _contentService.Save(content);
        }

        return content;
    }

    private void AssignDomainsToContent(IContent content)
    {
        // Assign a domain for each language to this root content
        // This allows the content to be accessible via different hostnames/paths per culture
        var existingDomains = _domainService.GetAssignedDomains(content.Id, false).ToList();

        foreach (var language in _allLanguages)
        {
            // Check if domain already exists for this language
            if (existingDomains.Any(d => d.LanguageIsoCode == language.IsoCode))
                continue;

            // Create a unique domain name for each root content and language
            // Format: test-section-{contentId}-{culture}.localhost
            var domainName = $"test-{content.Id}-{language.IsoCode.ToLower()}.localhost";

            var domain = new UmbracoDomain(domainName)
            {
                RootContentId = content.Id,
                LanguageIsoCode = language.IsoCode
            };

            _domainService.Save(domain);
        }

        Console.WriteLine($"Assigned {_allLanguages.Count} domains to root content '{content.Name}' (ID: {content.Id})");
    }

    private void SetSimpleProperties(IContent content, string? culture)
    {
        // Properties: title, description, isPublished
        if (content.HasProperty("title"))
        {
            content.SetValue("title", _faker.Lorem.Sentence(3), culture);
        }
        if (content.HasProperty("description"))
        {
            content.SetValue("description", _faker.Lorem.Paragraph(), culture);
        }
        if (content.HasProperty("isPublished"))
        {
            content.SetValue("isPublished", _faker.Random.Bool());
        }
    }

    private void SetMediumProperties(IContent content, string? culture)
    {
        // Properties: title, subtitle, summary, relatedContent, blocks
        if (content.HasProperty("title"))
        {
            content.SetValue("title", _faker.Lorem.Sentence(3), culture);
        }
        if (content.HasProperty("subtitle"))
        {
            content.SetValue("subtitle", _faker.Lorem.Sentence(5), culture);
        }
        if (content.HasProperty("summary"))
        {
            content.SetValue("summary", _faker.Lorem.Paragraph(), culture);
        }

        // Link to 2-5 random content items
        if (content.HasProperty("relatedContent") && _createdContent.Count > 0)
        {
            var randomContent = _createdContent[_faker.Random.Int(0, _createdContent.Count - 1)];
            content.SetValue("relatedContent", Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString());
        }

        // Add 1 simple block item to "blocks" property
        if (content.HasProperty("blocks"))
        {
            var blockJson = GenerateBlockListJsonWithSimpleBlock(content, "blocks");
            if (!string.IsNullOrEmpty(blockJson))
            {
                content.SetValue("blocks", blockJson);
            }
        }
    }

    private void SetComplexProperties(IContent content, string? culture)
    {
        // Properties: title, subtitle, bodyText, mainImage, thumbnailImage, primaryContent, secondaryContent, tertiaryContent, blocks
        if (content.HasProperty("title"))
        {
            content.SetValue("title", _faker.Lorem.Sentence(3), culture);
        }
        if (content.HasProperty("subtitle"))
        {
            content.SetValue("subtitle", _faker.Lorem.Sentence(5), culture);
        }
        if (content.HasProperty("bodyText"))
        {
            content.SetValue("bodyText", _faker.Lorem.Paragraphs(3), culture);
        }

        // Set media picker values
        if (_mediaItems.Count > 0)
        {
            if (content.HasProperty("mainImage"))
            {
                var randomMedia = _mediaItems[_faker.Random.Int(0, _mediaItems.Count - 1)];
                var mediaPickerValue = GenerateMediaPickerValue(randomMedia);
                content.SetValue("mainImage", mediaPickerValue);
            }
            if (content.HasProperty("thumbnailImage"))
            {
                var randomMedia = _mediaItems[_faker.Random.Int(0, _mediaItems.Count - 1)];
                var mediaPickerValue = GenerateMediaPickerValue(randomMedia);
                content.SetValue("thumbnailImage", mediaPickerValue);
            }
        }

        // Link to content items
        if (_createdContent.Count > 0)
        {
            if (content.HasProperty("primaryContent"))
            {
                var randomContent = _createdContent[_faker.Random.Int(0, _createdContent.Count - 1)];
                content.SetValue("primaryContent", Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString());
            }
            if (content.HasProperty("secondaryContent"))
            {
                var randomContent = _createdContent[_faker.Random.Int(0, _createdContent.Count - 1)];
                content.SetValue("secondaryContent", Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString());
            }
            if (content.HasProperty("tertiaryContent"))
            {
                var randomContent = _createdContent[_faker.Random.Int(0, _createdContent.Count - 1)];
                content.SetValue("tertiaryContent", Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString());
            }
        }

        // Add 1 simple + 1 medium + 1 complex block items to headerBlocks
        if (content.HasProperty("headerBlocks"))
        {
            var blockJson = GenerateBlockListJsonWithAllComplexities(content, "headerBlocks");
            if (!string.IsNullOrEmpty(blockJson))
            {
                content.SetValue("headerBlocks", blockJson);
            }
        }
        // Add 1 simple + 1 medium + 1 complex block items to footerBlocks
        if (content.HasProperty("footerBlocks"))
        {
            var blockJson = GenerateBlockListJsonWithAllComplexities(content, "footerBlocks");
            if (!string.IsNullOrEmpty(blockJson))
            {
                content.SetValue("footerBlocks", blockJson);
            }
        }

        // Add 1 simple + 1 medium + 1 complex block items to mainGrid
        if (content.HasProperty("mainGrid"))
        {
            var blockGridJson = GenerateBlockGridJsonWithAllComplexities(content, "mainGrid");
            if (!string.IsNullOrEmpty(blockGridJson))
            {
                content.SetValue("mainGrid", blockGridJson);
            }
        }
    }

    private string BuildElementContentData(IContentType elementType, Guid elementTypeKey, string contentUdi)
    {
        var properties = new List<string>
        {
            $"\"contentTypeKey\": \"{elementTypeKey}\"",
            $"\"udi\": \"umb://element/{contentUdi}\""
        };

        // Get all properties from all groups
        foreach (var group in elementType.PropertyGroups)
        {
            if (group.PropertyTypes == null) continue;

            foreach (var propType in group.PropertyTypes)
            {
                var propValue = GeneratePropertyValue(propType);
                if (propValue != null)
                {
                    properties.Add($"\"{propType.Alias}\": {propValue}");
                }
            }
        }

        // Also check non-grouped properties
        foreach (var propType in elementType.PropertyTypes)
        {
            // Skip if already added from groups
            if (properties.Any(p => p.Contains($"\"{propType.Alias}\":"))) continue;

            var propValue = GeneratePropertyValue(propType);
            if (propValue != null)
            {
                properties.Add($"\"{propType.Alias}\": {propValue}");
            }
        }

        return "{\n            " + string.Join(",\n            ", properties) + "\n        }";
    }

    private string? GeneratePropertyValue(IPropertyType propType)
    {
        // Get the data type to determine the property editor
        var dataType = _dataTypeService.GetDataType(propType.DataTypeId);
        if (dataType == null) return "\"\"";

        // Generate value based on property editor alias
        return dataType.EditorAlias switch
        {
            Constants.PropertyEditors.Aliases.TextBox => $"\"{_faker.Lorem.Sentence(3).Replace("\"", "\\\"")}\"",
            Constants.PropertyEditors.Aliases.TextArea => $"\"{_faker.Lorem.Paragraph().Replace("\"", "\\\"")}\"",
            Constants.PropertyEditors.Aliases.Boolean => "\"1\"",
            Constants.PropertyEditors.Aliases.Label => "\"\"",
            Constants.PropertyEditors.Aliases.Integer => $"{_faker.Random.Int(1, 100)}",
            _ => "\"\""
        };
    }

    /// <summary>
    /// Generate Media Picker 3 value JSON for a media item
    /// </summary>
    private string GenerateMediaPickerValue(IMedia media)
    {
        // Media Picker 3 expects JSON array format
        return $"[{{\"key\":\"{Guid.NewGuid()}\",\"mediaKey\":\"{media.Key}\"}}]";
    }

    /// <summary>
    /// Generate Block List JSON with only 1 simple block (for Medium content)
    /// </summary>
    private string? GenerateBlockListJsonWithSimpleBlock(IContent content, string propertyAlias)
    {
        var blockListConfig = GetBlockListConfiguration(content, propertyAlias);
        if (blockListConfig == null) return null;

        var blocks = blockListConfig.Blocks;
        if (blocks == null || blocks.Length == 0) return null;

        // Find a simple block
        var simpleBlock = FindBlockByComplexity(blocks, "Simple");
        if (simpleBlock == null) return null;

        var elementType = _contentTypeService.Get(simpleBlock.ContentElementTypeKey);
        if (elementType == null) return null;

        var contentUdi = Guid.NewGuid().ToString("N");
        var contentDataJson = BuildElementContentData(elementType, simpleBlock.ContentElementTypeKey, contentUdi);
        var layoutItem = $$"""{"contentUdi": "umb://element/{{contentUdi}}"}""";

        return $$"""
        {
            "layout": {
                "Umbraco.BlockList": [{{layoutItem}}]
            },
            "contentData": [{{contentDataJson}}],
            "settingsData": []
        }
        """;
    }

    /// <summary>
    /// Generate Block List JSON with 1 simple + 1 medium + 1 complex block (for Complex content)
    /// </summary>
    private string? GenerateBlockListJsonWithAllComplexities(IContent content, string propertyAlias)
    {
        var blockListConfig = GetBlockListConfiguration(content, propertyAlias);
        if (blockListConfig == null) return null;

        var blocks = blockListConfig.Blocks;
        if (blocks == null || blocks.Length == 0) return null;

        var layoutItems = new List<string>();
        var contentDataItems = new List<string>();

        // Add simple block
        var simpleBlock = FindBlockByComplexity(blocks, "Simple");
        if (simpleBlock != null)
        {
            var elementType = _contentTypeService.Get(simpleBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                contentDataItems.Add(BuildElementContentData(elementType, simpleBlock.ContentElementTypeKey, contentUdi));
                layoutItems.Add($$"""{"contentUdi": "umb://element/{{contentUdi}}"}""");
            }
        }

        // Add medium block
        var mediumBlock = FindBlockByComplexity(blocks, "Medium");
        if (mediumBlock != null)
        {
            var elementType = _contentTypeService.Get(mediumBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                contentDataItems.Add(BuildElementContentData(elementType, mediumBlock.ContentElementTypeKey, contentUdi));
                layoutItems.Add($$"""{"contentUdi": "umb://element/{{contentUdi}}"}""");
            }
        }

        // Add complex block
        var complexBlock = FindBlockByComplexity(blocks, "Complex");
        if (complexBlock != null)
        {
            var elementType = _contentTypeService.Get(complexBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                contentDataItems.Add(BuildElementContentData(elementType, complexBlock.ContentElementTypeKey, contentUdi));
                layoutItems.Add($$"""{"contentUdi": "umb://element/{{contentUdi}}"}""");
            }
        }

        if (layoutItems.Count == 0) return null;

        return $$"""
        {
            "layout": {
                "Umbraco.BlockList": [{{string.Join(", ", layoutItems)}}]
            },
            "contentData": [{{string.Join(", ", contentDataItems)}}],
            "settingsData": []
        }
        """;
    }

    /// <summary>
    /// Generate Block Grid JSON with 1 simple + 1 medium + 1 complex block (for Complex content)
    /// </summary>
    private string? GenerateBlockGridJsonWithAllComplexities(IContent content, string propertyAlias)
    {
        var blockGridConfig = GetBlockGridConfiguration(content, propertyAlias);
        if (blockGridConfig == null) return null;

        var blocks = blockGridConfig.Blocks;
        if (blocks == null || blocks.Length == 0) return null;

        var layoutItems = new List<string>();
        var contentDataItems = new List<string>();

        // Add simple block
        var simpleBlock = FindBlockGridByComplexity(blocks, "Simple");
        if (simpleBlock != null)
        {
            var elementType = _contentTypeService.Get(simpleBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                contentDataItems.Add(BuildElementContentData(elementType, simpleBlock.ContentElementTypeKey, contentUdi));
                layoutItems.Add($$"""
                {
                    "contentUdi": "umb://element/{{contentUdi}}",
                    "columnSpan": 12,
                    "rowSpan": 1,
                    "areas": []
                }
                """);
            }
        }

        // Add medium block
        var mediumBlock = FindBlockGridByComplexity(blocks, "Medium");
        if (mediumBlock != null)
        {
            var elementType = _contentTypeService.Get(mediumBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                contentDataItems.Add(BuildElementContentData(elementType, mediumBlock.ContentElementTypeKey, contentUdi));
                layoutItems.Add($$"""
                {
                    "contentUdi": "umb://element/{{contentUdi}}",
                    "columnSpan": 12,
                    "rowSpan": 1,
                    "areas": []
                }
                """);
            }
        }

        // Add complex block
        var complexBlock = FindBlockGridByComplexity(blocks, "Complex");
        if (complexBlock != null)
        {
            var elementType = _contentTypeService.Get(complexBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                contentDataItems.Add(BuildElementContentData(elementType, complexBlock.ContentElementTypeKey, contentUdi));
                layoutItems.Add($$"""
                {
                    "contentUdi": "umb://element/{{contentUdi}}",
                    "columnSpan": 12,
                    "rowSpan": 1,
                    "areas": []
                }
                """);
            }
        }

        if (layoutItems.Count == 0) return null;

        return $$"""
        {
            "layout": {
                "Umbraco.BlockGrid": [{{string.Join(", ", layoutItems)}}]
            },
            "contentData": [{{string.Join(", ", contentDataItems)}}],
            "settingsData": []
        }
        """;
    }

    /// <summary>
    /// Get BlockList configuration for a property
    /// </summary>
    private BlockListConfiguration? GetBlockListConfiguration(IContent content, string propertyAlias)
    {
        var contentType = _contentTypeService.Get(content.ContentTypeId);
        if (contentType == null) return null;

        IPropertyType? propertyType = null;
        foreach (var group in contentType.PropertyGroups)
        {
            propertyType = group.PropertyTypes?.FirstOrDefault(pt => pt.Alias == propertyAlias);
            if (propertyType != null) break;
        }
        propertyType ??= contentType.PropertyTypes.FirstOrDefault(pt => pt.Alias == propertyAlias);
        if (propertyType == null) return null;

        var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
        return dataType?.Configuration as BlockListConfiguration;
    }

    /// <summary>
    /// Get BlockGrid configuration for a property
    /// </summary>
    private BlockGridConfiguration? GetBlockGridConfiguration(IContent content, string propertyAlias)
    {
        var contentType = _contentTypeService.Get(content.ContentTypeId);
        if (contentType == null) return null;

        IPropertyType? propertyType = null;
        foreach (var group in contentType.PropertyGroups)
        {
            propertyType = group.PropertyTypes?.FirstOrDefault(pt => pt.Alias == propertyAlias);
            if (propertyType != null) break;
        }
        propertyType ??= contentType.PropertyTypes.FirstOrDefault(pt => pt.Alias == propertyAlias);
        if (propertyType == null) return null;

        var dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
        return dataType?.Configuration as BlockGridConfiguration;
    }

    /// <summary>
    /// Find a block by complexity type (Simple, Medium, Complex)
    /// </summary>
    private BlockListConfiguration.BlockConfiguration? FindBlockByComplexity(BlockListConfiguration.BlockConfiguration[] blocks, string complexity)
    {
        foreach (var block in blocks)
        {
            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType != null && elementType.Alias.Contains(complexity, StringComparison.OrdinalIgnoreCase))
            {
                return block;
            }
        }
        return null;
    }

    /// <summary>
    /// Find a block grid block by complexity type (Simple, Medium, Complex)
    /// </summary>
    private BlockGridConfiguration.BlockGridBlockConfiguration? FindBlockGridByComplexity(BlockGridConfiguration.BlockGridBlockConfiguration[] blocks, string complexity)
    {
        foreach (var block in blocks)
        {
            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType != null && elementType.Alias.Contains(complexity, StringComparison.OrdinalIgnoreCase))
            {
                return block;
            }
        }
        return null;
    }
}
