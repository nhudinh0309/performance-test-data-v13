namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;
using static Umbraco.Community.PerformanceTestDataSeeder.Infrastructure.SeederConstants;

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

    // Performance caches to avoid repeated lookups (cleared at start of each seeding run)
    private readonly Dictionary<int, IContentType> _contentTypeCache = new();
    private readonly Dictionary<Guid, IContentType?> _contentTypeByKeyCache = new();
    private readonly Dictionary<(int contentTypeId, string propertyAlias), BlockListConfiguration?> _blockListConfigCache = new();
    private readonly Dictionary<(int contentTypeId, string propertyAlias), BlockGridConfiguration?> _blockGridConfigCache = new();

    // Batch publishing: tracks content items pending publication
    private readonly List<(IContent Content, bool IsVariant)> _pendingPublishContent = new();

    /// <summary>
    /// Clears all internal caches. Called at the start of seeding to ensure fresh data.
    /// </summary>
    private void ClearCaches()
    {
        _contentTypeCache.Clear();
        _contentTypeByKeyCache.Clear();
        _blockListConfigCache.Clear();
        _blockGridConfigCache.Clear();
        _pendingPublishContent.Clear();
        Logger.LogDebug("Cleared ContentSeeder caches");
    }

    /// <summary>
    /// Creates a new ContentSeeder instance.
    /// </summary>
    public ContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ILocalizationService localizationService,
        IDomainService domainService,
        IScopeProvider scopeProvider,
        ILogger<ContentSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
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
        var prefix = GetPrefix(PrefixType.Content);
        var rootContent = _contentService.GetRootContent();
        return rootContent.Any(c => c.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        // Clear caches to ensure fresh data (prevents stale cache if run multiple times)
        ClearCaches();

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
        var variantPrefix = GetPrefix(PrefixType.VariantDocType);
        var invariantPrefix = GetPrefix(PrefixType.InvariantDocType);

        Context.AddSimpleDocTypes(allTypes.Where(t =>
            t.Alias.StartsWith($"{variantPrefix}Simple", StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith($"{invariantPrefix}Simple", StringComparison.OrdinalIgnoreCase)));

        Context.AddMediumDocTypes(allTypes.Where(t =>
            t.Alias.StartsWith($"{variantPrefix}Medium", StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith($"{invariantPrefix}Medium", StringComparison.OrdinalIgnoreCase)));

        Context.AddComplexDocTypes(allTypes.Where(t =>
            t.Alias.StartsWith($"{variantPrefix}Complex", StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith($"{invariantPrefix}Complex", StringComparison.OrdinalIgnoreCase)));

        Logger.LogDebug("Loaded doc types - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}",
            Context.SimpleDocTypes.Count, Context.MediumDocTypes.Count, Context.ComplexDocTypes.Count);
    }

    private void LoadLanguagesIfNeeded()
    {
        if (Context.Languages.Count > 0) return;
        Context.SetLanguages(_localizationService.GetAllLanguages());
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
        var prefix = GetPrefix(PrefixType.Content);

        int totalCreated = 0;
        int targetTotal = contentConfig.TotalTarget;

        int simpleTarget = contentConfig.SimpleTarget;
        int mediumTarget = contentConfig.MediumTarget;
        int complexTarget = contentConfig.ComplexTarget;

        int simpleCreated = 0;
        int mediumCreated = 0;
        int complexCreated = 0;

        // DryRun summary tracking
        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create content tree with target: {Target} items", targetTotal);
            Logger.LogInformation("[DRY-RUN] Distribution - Simple: {Simple}%, Medium: {Medium}%, Complex: {Complex}%",
                contentConfig.SimplePercent, contentConfig.MediumPercent, contentConfig.ComplexPercent);
        }

        int batchCount = 0;
        IScope? currentScope = null;

        try
        {
            // Create root sections
            for (int section = 1; section <= contentConfig.RootSections && totalCreated < targetTotal; section++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Start new batch scope if needed (only when persisting)
                    if (currentScope == null && ShouldPersist)
                    {
                        currentScope = CreateScopedBatch();
                        batchCount = 0;
                    }

                    var sectionDocType = GetRandomDocType("simple");
                    var sectionContent = CreateContent($"{prefix}Section_{section}", -1, sectionDocType, "simple");
                    if (sectionContent != null) Context.AddContent(sectionContent);
                    simpleCreated++;
                    totalCreated++;
                    batchCount++;

                    // Each section has categories
                    for (int cat = 1; cat <= contentConfig.CategoriesPerSection && totalCreated < targetTotal; cat++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var catDocType = GetRandomDocType("simple");
                        var parentId = sectionContent?.Id ?? -1;
                        var catContent = CreateContent($"Category_{section}_{cat}", parentId, catDocType, "simple");
                        if (catContent != null) Context.AddContent(catContent);
                        simpleCreated++;
                        totalCreated++;
                        batchCount++;

                        // Check if we should commit the batch (with proper error handling)
                        if (ShouldPersist && batchCount >= Options.BatchSize)
                        {
                            CommitAndResetBatch(ref currentScope, ref batchCount);
                            PublishPendingContentBatch(); // Batch publish if threshold reached
                        }

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

                            var catParentId = catContent?.Id ?? -1;
                            var pageContent = CreateContent($"Page_{section}_{cat}_{page}", catParentId, docType, complexity);
                            if (pageContent != null) Context.AddContent(pageContent);
                            totalCreated++;
                            batchCount++;

                            // Check if we should commit the batch
                            if (ShouldPersist && batchCount >= Options.BatchSize)
                            {
                                CommitAndResetBatch(ref currentScope, ref batchCount);
                                PublishPendingContentBatch(); // Batch publish if threshold reached
                            }

                            // Some pages have detail children (Level 4)
                            if (page % 5 == 0 && totalCreated < targetTotal)
                            {
                                int detailsPerPage = Math.Min(8, targetTotal - totalCreated);
                                for (int detail = 1; detail <= detailsPerPage && totalCreated < targetTotal; detail++)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    var detailDocType = GetRandomDocType("simple");
                                    var pageParentId = pageContent?.Id ?? -1;
                                    var detailContent = CreateContent($"Detail_{section}_{cat}_{page}_{detail}",
                                        pageParentId, detailDocType, "simple");
                                    if (detailContent != null) Context.AddContent(detailContent);
                                    simpleCreated++;
                                    totalCreated++;
                                    batchCount++;

                                    // Check if we should commit the batch
                                    if (ShouldPersist && batchCount >= Options.BatchSize)
                                    {
                                        CommitAndResetBatch(ref currentScope, ref batchCount);
                                        PublishPendingContentBatch(); // Batch publish if threshold reached
                                    }
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

            // Complete any remaining batch
            if (currentScope != null && batchCount > 0)
            {
                currentScope.Complete();
            }
        }
        finally
        {
            currentScope?.Dispose();
        }

        // Publish any remaining pending content
        if (_pendingPublishContent.Count > 0)
        {
            Logger.LogInformation("Publishing remaining {Count} content items...", _pendingPublishContent.Count);
            PublishPendingContentBatch(force: true);
        }

        Logger.LogInformation("Final count - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}, Total: {Total}",
            simpleCreated, mediumCreated, complexCreated, totalCreated);
    }

    /// <summary>
    /// Safely commits the current batch and creates a new scope.
    /// Ensures proper disposal even if scope creation fails.
    /// </summary>
    private void CommitAndResetBatch(ref IScope? currentScope, ref int batchCount)
    {
        if (currentScope == null) return;

        try
        {
            currentScope.Complete();
        }
        finally
        {
            currentScope.Dispose();
            currentScope = null;
        }

        // Create new scope (if this fails, currentScope is already null and cleaned up)
        currentScope = CreateScopedBatch();
        batchCount = 0;
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

        // If all targets met, rotate through available types based on what has doc types
        if (totalRemaining == 0)
        {
            // Prefer complex, then medium, then simple (to avoid over-creating simple)
            if (Context.ComplexDocTypes.Count > 0)
                return ("complex", GetRandomDocType("complex"));
            if (Context.MediumDocTypes.Count > 0)
                return ("medium", GetRandomDocType("medium"));
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

    private IContent? CreateContent(string name, int parentId, IContentType docType, string complexity)
    {
        // DryRun mode - log what would be created but don't persist
        if (IsDryRun)
        {
            LogDryRun("Content", name, $"type={docType.Alias}, complexity={complexity}, parent={parentId}");
            return null;
        }

        var content = _contentService.Create(name, parentId, docType);
        var isVariant = docType.Variations.HasFlag(ContentVariation.Culture);
        var isRootContent = parentId == -1;
        var useBatchPublishing = Options.PublishContent && Options.PublishBatchSize > 0;

        if (isVariant)
        {
            // For variant content, set name and properties for ALL cultures
            foreach (var language in Context.Languages)
            {
                var culture = language.IsoCode;
                content.SetCultureName($"{name} ({culture})", culture);

                SetPropertiesForCulture(content, complexity, culture);
            }

            // Save content (publish later if batch mode, or now if immediate mode)
            if (Options.PublishContent && !useBatchPublishing)
            {
                // Immediate publish mode (legacy behavior when PublishBatchSize = 0)
                var cultures = Context.Languages.Select(l => l.IsoCode).ToArray();
                var result = _contentService.SaveAndPublish(content, cultures);
                if (!result.Success)
                {
                    Logger.LogWarning("Failed to publish variant content {Name}: {Messages}",
                        content.Name ?? "Unknown", string.Join(", ", result.EventMessages?.GetAll().Select(m => m.Message) ?? Array.Empty<string>()));
                }
            }
            else
            {
                // Save without publishing (will batch publish later if enabled)
                _contentService.Save(content);

                if (useBatchPublishing)
                {
                    _pendingPublishContent.Add((content, true));
                }
            }

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

            if (Options.PublishContent && !useBatchPublishing)
            {
                // Immediate publish mode (legacy behavior when PublishBatchSize = 0)
                var result = _contentService.SaveAndPublish(content);
                if (!result.Success)
                {
                    Logger.LogWarning("Failed to publish invariant content {Name}: {Messages}",
                        content.Name ?? "Unknown", string.Join(", ", result.EventMessages?.GetAll().Select(m => m.Message) ?? Array.Empty<string>()));
                }
            }
            else
            {
                // Save without publishing (will batch publish later if enabled)
                _contentService.Save(content);

                if (useBatchPublishing)
                {
                    _pendingPublishContent.Add((content, false));
                }
            }
        }

        return content;
    }

    /// <summary>
    /// Publishes pending content items in batches for better performance.
    /// </summary>
    /// <param name="force">If true, publishes all remaining items regardless of batch size.</param>
    private void PublishPendingContentBatch(bool force = false)
    {
        if (!Options.PublishContent || Options.PublishBatchSize <= 0)
            return;

        if (!force && _pendingPublishContent.Count < Options.PublishBatchSize)
            return;

        if (_pendingPublishContent.Count == 0)
            return;

        var itemsToPublish = force
            ? _pendingPublishContent.ToList()
            : _pendingPublishContent.Take(Options.PublishBatchSize).ToList();

        var cultures = Context.Languages.Select(l => l.IsoCode).ToArray();
        int successCount = 0;
        int failCount = 0;

        using var scope = CreateScopedBatch();

        foreach (var (content, isVariant) in itemsToPublish)
        {
            try
            {
                var result = isVariant
                    ? _contentService.SaveAndPublish(content, cultures)
                    : _contentService.SaveAndPublish(content);

                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    Logger.LogDebug("Failed to publish content {Name}: {Messages}",
                        content.Name ?? "Unknown",
                        string.Join(", ", result.EventMessages?.GetAll().Select(m => m.Message) ?? Array.Empty<string>()));
                }
            }
            catch (Exception ex)
            {
                failCount++;
                Logger.LogWarning(ex, "Exception publishing content {Name}", content.Name ?? "Unknown");
                if (Options.StopOnError) throw;
            }
        }

        scope.Complete();

        // Remove published items from pending list
        foreach (var item in itemsToPublish)
        {
            _pendingPublishContent.Remove(item);
        }

        Logger.LogDebug("Batch published {Success} content items ({Failed} failed, {Remaining} remaining)",
            successCount, failCount, _pendingPublishContent.Count);
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
        // Skip domain assignment if configured
        if (Options.SkipContentDomains)
        {
            Logger.LogDebug("Skipping domain assignment for '{Name}' (SkipContentDomains=true)", content.Name);
            return;
        }

        var existingDomains = _domainService.GetAssignedDomains(content.Id, false).ToList();
        var domainSuffix = Options.DomainSuffix;

        foreach (var language in Context.Languages)
        {
            if (existingDomains.Any(d => d.LanguageIsoCode == language.IsoCode))
                continue;

            var domainName = $"test-{content.Id}-{language.IsoCode.ToLower()}.{domainSuffix}";

            var domain = new UmbracoDomain(domainName)
            {
                RootContentId = content.Id,
                LanguageIsoCode = language.IsoCode
            };

            _domainService.Save(domain);
        }

        Logger.LogDebug("Assigned {Count} domains to root content '{Name}' (ID: {Id}) with suffix '{Suffix}'",
            Context.Languages.Count, content.Name, content.Id, domainSuffix);
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

    /// <summary>
    /// Generates the correct JSON format for Umbraco MediaPicker3.
    /// Format: [{ "key": "guid", "mediaKey": "media-guid" }]
    /// </summary>
    private static string GenerateMediaPickerValue(IMedia media)
    {
        // MediaPicker3 expects an array of objects with key and mediaKey properties
        var mediaPickerItems = new[]
        {
            new
            {
                key = Guid.NewGuid().ToString(),
                mediaKey = media.Key.ToString()
            }
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

        // Add nested block element first (if available)
        var nestedBlock = FindNestedBlockElement(blockConfig.Blocks);
        if (nestedBlock != null)
        {
            var elementType = _contentTypeService.Get(nestedBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                var nestedContentData = BuildNestedBlockContentDataObject(elementType, nestedBlock.ContentElementTypeKey, contentUdi, 1);
                contentDataItems.Add(nestedContentData);
                layoutItems.Add(new { contentUdi = $"umb://element/{contentUdi}" });
            }
        }

        // Add regular complexity blocks
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

        // Add nested block element first (if available)
        var nestedBlock = FindNestedBlockGridElement(blockConfig.Blocks);
        if (nestedBlock != null)
        {
            var elementType = _contentTypeService.Get(nestedBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                var nestedContentData = BuildNestedBlockContentDataObject(elementType, nestedBlock.ContentElementTypeKey, contentUdi, 1);
                contentDataItems.Add(nestedContentData);
                layoutItems.Add(new
                {
                    contentUdi = $"umb://element/{contentUdi}",
                    columnSpan = DefaultGridColumnSpan,
                    rowSpan = DefaultGridRowSpan,
                    areas = Array.Empty<object>()
                });
            }
        }

        // Add regular complexity blocks
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
                columnSpan = DefaultGridColumnSpan,
                rowSpan = DefaultGridRowSpan,
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

    /// <summary>
    /// Builds content data for nested block elements, recursively generating child blocks.
    /// Uses configured NestingDepth from DocumentTypes configuration.
    /// </summary>
    private Dictionary<string, object> BuildNestedBlockContentDataObject(
        IContentType elementType, Guid elementTypeKey, string contentUdi, int currentDepth, int? maxDepth = null)
    {
        // Use configured NestingDepth if not explicitly provided
        var effectiveMaxDepth = maxDepth ?? Config.DocumentTypes.NestingDepth;

        // Explicit recursion guard to prevent stack overflow
        const int absoluteMaxDepth = 50;
        if (currentDepth > effectiveMaxDepth || currentDepth > absoluteMaxDepth)
        {
            Logger.LogDebug("Recursion depth limit reached ({CurrentDepth}/{MaxDepth}), stopping nested block generation",
                currentDepth, effectiveMaxDepth);
            return new Dictionary<string, object>
            {
                ["contentTypeKey"] = elementTypeKey.ToString(),
                ["udi"] = $"umb://element/{contentUdi}"
            };
        }

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
                // Handle nestedBlocks property specially - generate nested content
                if (propType.Alias == "nestedBlocks" && currentDepth < effectiveMaxDepth)
                {
                    var nestedBlockListJson = GenerateNestedBlockListJson(propType, currentDepth + 1, effectiveMaxDepth);
                    if (!string.IsNullOrEmpty(nestedBlockListJson))
                        properties[propType.Alias] = nestedBlockListJson;
                }
                else
                {
                    var propValue = GeneratePropertyValue(propType);
                    if (propValue != null)
                        properties[propType.Alias] = propValue;
                }
            }
        }

        return properties;
    }

    /// <summary>
    /// Generates BlockList JSON for nested blocks property.
    /// </summary>
    private string? GenerateNestedBlockListJson(IPropertyType propType, int currentDepth, int maxDepth)
    {
        // Explicit recursion guard
        const int absoluteMaxDepth = 50;
        if (currentDepth > maxDepth || currentDepth > absoluteMaxDepth)
        {
            return null;
        }

        // Get the BlockList configuration for this property
        if (!Context.DataTypeCache.TryGetValue(propType.DataTypeId, out var dataType))
        {
            dataType = _dataTypeService.GetDataType(propType.DataTypeId);
            if (dataType != null)
                Context.DataTypeCache[propType.DataTypeId] = dataType;
        }

        if (dataType?.Configuration is not BlockListConfiguration blockConfig)
            return null;

        if (blockConfig.Blocks == null || blockConfig.Blocks.Length == 0)
            return null;

        var layoutItems = new List<object>();
        var contentDataItems = new List<Dictionary<string, object>>();

        // Check if there are more nested containers at this level
        var nestedBlock = FindNestedBlockElement(blockConfig.Blocks);
        if (nestedBlock != null && currentDepth < maxDepth)
        {
            var elementType = _contentTypeService.Get(nestedBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentUdi = Guid.NewGuid().ToString("N");
                var nestedContentData = BuildNestedBlockContentDataObject(
                    elementType, nestedBlock.ContentElementTypeKey, contentUdi, currentDepth, maxDepth);
                contentDataItems.Add(nestedContentData);
                layoutItems.Add(new { contentUdi = $"umb://element/{contentUdi}" });
            }
        }

        // Add leaf elements (Simple/Medium/Complex)
        foreach (var complexity in new[] { "Simple", "Medium" })
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
            Constants.PropertyEditors.Aliases.MediaPicker3 => GenerateMediaPickerValueForBlock(),
            Constants.PropertyEditors.Aliases.ContentPicker => GenerateContentPickerValueForBlock(),
            _ => string.Empty
        };
    }

    private object? GenerateMediaPickerValueForBlock()
    {
        if (Context.MediaItems.Count == 0) return null;

        var randomMedia = Context.MediaItems[Context.Random.Next(Context.MediaItems.Count)];
        return GenerateMediaPickerValue(randomMedia);
    }

    private object? GenerateContentPickerValueForBlock()
    {
        if (Context.CreatedContent.Count == 0) return null;

        var randomContent = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
        return Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString();
    }

    /// <summary>
    /// Gets content type with caching to avoid repeated DB lookups.
    /// </summary>
    private IContentType? GetCachedContentType(int contentTypeId)
    {
        if (_contentTypeCache.TryGetValue(contentTypeId, out var cached))
            return cached;

        var contentType = _contentTypeService.Get(contentTypeId);
        if (contentType != null)
            _contentTypeCache[contentTypeId] = contentType;

        return contentType;
    }

    private BlockListConfiguration? GetBlockListConfiguration(IContent content, string propertyAlias)
    {
        var cacheKey = (content.ContentTypeId, propertyAlias);

        // Check cache first
        if (_blockListConfigCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var contentType = GetCachedContentType(content.ContentTypeId);
        if (contentType == null)
        {
            _blockListConfigCache[cacheKey] = null;
            return null;
        }

        var propertyType = FindPropertyType(contentType, propertyAlias);
        if (propertyType == null)
        {
            _blockListConfigCache[cacheKey] = null;
            return null;
        }

        // Use data type cache from context
        if (!Context.DataTypeCache.TryGetValue(propertyType.DataTypeId, out var dataType))
        {
            dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
            if (dataType != null)
                Context.DataTypeCache[propertyType.DataTypeId] = dataType;
        }

        var config = dataType?.Configuration as BlockListConfiguration;
        _blockListConfigCache[cacheKey] = config;
        return config;
    }

    private BlockGridConfiguration? GetBlockGridConfiguration(IContent content, string propertyAlias)
    {
        var cacheKey = (content.ContentTypeId, propertyAlias);

        // Check cache first
        if (_blockGridConfigCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var contentType = GetCachedContentType(content.ContentTypeId);
        if (contentType == null)
        {
            _blockGridConfigCache[cacheKey] = null;
            return null;
        }

        var propertyType = FindPropertyType(contentType, propertyAlias);
        if (propertyType == null)
        {
            _blockGridConfigCache[cacheKey] = null;
            return null;
        }

        // Use data type cache from context
        if (!Context.DataTypeCache.TryGetValue(propertyType.DataTypeId, out var dataType))
        {
            dataType = _dataTypeService.GetDataType(propertyType.DataTypeId);
            if (dataType != null)
                Context.DataTypeCache[propertyType.DataTypeId] = dataType;
        }

        var config = dataType?.Configuration as BlockGridConfiguration;
        _blockGridConfigCache[cacheKey] = config;
        return config;
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

    #region Block Finding Helpers

    /// <summary>
    /// Gets content type by key with caching to avoid repeated DB lookups.
    /// </summary>
    private IContentType? GetCachedContentTypeByKey(Guid key)
    {
        if (_contentTypeByKeyCache.TryGetValue(key, out var cached))
            return cached;

        var contentType = _contentTypeService.Get(key);
        _contentTypeByKeyCache[key] = contentType;

        return contentType;
    }

    /// <summary>
    /// Generic helper to find a block by matching its element type alias against a pattern.
    /// </summary>
    private T? FindBlockByAlias<T>(T[] blocks, Func<T, Guid> keySelector, string aliasPattern) where T : class
    {
        foreach (var block in blocks)
        {
            var elementType = GetCachedContentTypeByKey(keySelector(block));
            if (elementType?.Alias.Contains(aliasPattern, StringComparison.OrdinalIgnoreCase) == true)
                return block;
        }
        return null;
    }

    private BlockListConfiguration.BlockConfiguration? FindBlockByComplexity(
        BlockListConfiguration.BlockConfiguration[] blocks, string complexity)
        => FindBlockByAlias(blocks, b => b.ContentElementTypeKey, complexity);

    private BlockListConfiguration.BlockConfiguration? FindNestedBlockElement(
        BlockListConfiguration.BlockConfiguration[] blocks)
        => FindBlockByAlias(blocks, b => b.ContentElementTypeKey, "NestedBlock_Depth");

    private BlockGridConfiguration.BlockGridBlockConfiguration? FindBlockGridByComplexity(
        BlockGridConfiguration.BlockGridBlockConfiguration[] blocks, string complexity)
        => FindBlockByAlias(blocks, b => b.ContentElementTypeKey, complexity);

    private BlockGridConfiguration.BlockGridBlockConfiguration? FindNestedBlockGridElement(
        BlockGridConfiguration.BlockGridBlockConfiguration[] blocks)
        => FindBlockByAlias(blocks, b => b.ContentElementTypeKey, "NestedBlock_Depth");

    #endregion
}
