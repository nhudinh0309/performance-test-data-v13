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
    private readonly ILanguageService _languageService;
    private readonly IDomainService _domainService;
    private readonly IMediaService _mediaService;

    // Performance caches to avoid repeated lookups (cleared at start of each seeding run)
    private readonly Dictionary<int, IContentType> _contentTypeCache = new();
    private readonly Dictionary<Guid, IContentType?> _contentTypeByKeyCache = new();
    private readonly Dictionary<(int contentTypeId, string propertyAlias), BlockListConfiguration?> _blockListConfigCache = new();
    private readonly Dictionary<(int contentTypeId, string propertyAlias), BlockGridConfiguration?> _blockGridConfigCache = new();

    // Root content items needing domain assignment after creation scopes commit
    private readonly List<IContent> _rootContentForDomains = new();

    // Batch publishing: tracks content items pending publication
    private readonly List<(IContent Content, bool IsVariant)> _pendingPublishContent = new();

    // Track current section for FirstSection publish mode
    private int _currentSection = 0;

    /// <summary>
    /// Clears all internal caches. Called at the start of seeding to ensure fresh data.
    /// </summary>
    private void ClearCaches()
    {
        _contentTypeCache.Clear();
        _contentTypeByKeyCache.Clear();
        _blockListConfigCache.Clear();
        _blockGridConfigCache.Clear();
        _rootContentForDomains.Clear();
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
        ILanguageService languageService,
        IDomainService domainService,
        IMediaService mediaService,
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
        _languageService = languageService;
        _domainService = domainService;
        _mediaService = mediaService;
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
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Clear caches to ensure fresh data (prevents stale cache if run multiple times)
        ClearCaches();

        // Load document types if not already cached
        LoadDocumentTypesIfNeeded();

        if (Context.SimpleDocTypes.Count == 0)
        {
            Logger.LogWarning("No document types found. Please ensure DocumentTypeSeeder ran first.");
            return;
        }

        // Load languages if not already cached
        await LoadLanguagesIfNeededAsync();
        Logger.LogInformation("Using {Count} languages for content", Context.Languages.Count);

        // Load media items if not already cached
        LoadMediaItemsIfNeeded();

        // Pre-load all data types into cache to avoid repeated GetAllAsync calls
        await PreloadDataTypeCacheAsync();

        // Create content tree
        await CreateContentTreeAsync(cancellationToken);

        Logger.LogInformation("Content seeding completed! Created {Count} content nodes.", Context.CreatedContent.Count);
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

    private async Task LoadLanguagesIfNeededAsync()
    {
        if (Context.Languages.Count > 0) return;
        Context.SetLanguages(await _languageService.GetAllAsync());
    }

    /// <summary>
    /// Pre-loads all data types into Context.DataTypeCache to avoid repeated GetAllAsync calls
    /// during block content generation.
    /// </summary>
    private async Task PreloadDataTypeCacheAsync()
    {
        if (Context.DataTypeCache.Count > 0) return;

        var allDataTypes = await _dataTypeService.GetAllAsync(Array.Empty<Guid>());
        foreach (var dt in allDataTypes)
        {
            Context.DataTypeCache[dt.Id] = dt;
        }
        Logger.LogDebug("Pre-loaded {Count} data types into cache", Context.DataTypeCache.Count);
    }

    private void LoadMediaItemsIfNeeded()
    {
        if (Context.MediaItems.Count > 0) return;

        // MediaSeeder may have been skipped (already seeded), so load media from DB directly
        Logger.LogDebug("Media items not in context, loading from database...");

        var prefix = GetPrefix(PrefixType.Media);
        var rootMedia = _mediaService.GetRootMedia()
            .Where(m => m.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var imageMedia = new List<IMedia>();
        foreach (var root in rootMedia)
        {
            long pageIndex = 0;
            const int pageSize = 100;
            long totalRecords;

            do
            {
                var descendants = _mediaService.GetPagedDescendants(root.Id, pageIndex, pageSize, out totalRecords);
                var images = descendants.Where(m => m.ContentType.Alias == Constants.Conventions.MediaTypes.Image);
                imageMedia.AddRange(images);
                pageIndex++;
            } while (pageIndex * pageSize < totalRecords);
        }

        Context.AddMediaItems(imageMedia);
        Logger.LogInformation("Loaded {Count} media images from database for content linking", imageMedia.Count);
    }

    private async Task CreateContentTreeAsync(CancellationToken cancellationToken)
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
                _currentSection = section;

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
                                    }
                                }
                            }

                            LogProgress(totalCreated, targetTotal, "content nodes");
                        }
                    }

                    Logger.LogDebug("Completed root section {Section}/{TotalSections} - Total content created: {Created}/{Target}",
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

        // Assign domains to all root content AFTER creation scopes are committed
        if (_rootContentForDomains.Count > 0)
        {
            Logger.LogInformation("Assigning domains to {Count} root content items...", _rootContentForDomains.Count);

            // Warn once if DomainSuffix is the bare default without a port
            var domainSuffix = Options.DomainSuffix;
            if (!Options.SkipContentDomains &&
                (domainSuffix.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 domainSuffix.Equals("localhost/", StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogWarning(
                    "DomainSuffix is '{DomainSuffix}' (no port). Domains won't match requests on " +
                    "non-standard ports (e.g., localhost:44340). Set PerformanceTestDataSeeder:Options:DomainSuffix " +
                    "to include the port in appsettings.json",
                    domainSuffix);
            }

            foreach (var rootContent in _rootContentForDomains)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await AssignAllDomainsToContentAsync(rootContent);
            }
        }

        // Publish all pending content (after domains are assigned)
        if (_pendingPublishContent.Count > 0)
        {
            Logger.LogInformation("Publishing {Count} content items...", _pendingPublishContent.Count);
            PublishAllPendingContent(cancellationToken);
        }

        Logger.LogInformation("Final count - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}, Total: {Total}",
            simpleCreated, mediumCreated, complexCreated, totalCreated);
    }

    /// <summary>
    /// Publishes all pending content items in batches at the end of content creation.
    /// </summary>
    private void PublishAllPendingContent(CancellationToken cancellationToken)
    {
        if (Options.PublishMode == PublishMode.None || _pendingPublishContent.Count == 0)
            return;

        int totalToPublish = _pendingPublishContent.Count;
        int published = 0;
        int failed = 0;

        // Process in batches
        var batchSize = Options.PublishBatchSize > 0 ? Options.PublishBatchSize : 50;

        while (_pendingPublishContent.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = _pendingPublishContent.Take(batchSize).ToList();

            // Don't suppress notifications when publishing - cache needs to be updated
            using var scope = CreateScopedBatch(suppressNotifications: false);

            foreach (var (content, isVariant) in batch)
            {
                try
                {
                    var publishCultures = isVariant
                        ? Context.Languages.Select(l => l.IsoCode).ToArray()
                        : Array.Empty<string>();

                    var result = _contentService.Publish(content, publishCultures);

                    if (result.Success)
                    {
                        published++;
                    }
                    else
                    {
                        failed++;
                        Logger.LogDebug("Failed to publish content {Name}: {Messages}",
                            content.Name ?? "Unknown",
                            string.Join(", ", result.EventMessages?.GetAll().Select(m => m.Message) ?? Array.Empty<string>()));
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Logger.LogWarning(ex, "Exception publishing content {Name}", content.Name ?? "Unknown");
                    if (Options.StopOnError) throw;
                }
            }

            scope.Complete();

            // Remove published items from pending list
            foreach (var item in batch)
            {
                _pendingPublishContent.Remove(item);
            }

            LogProgress(published, totalToPublish, "content published");
        }

        Logger.LogInformation("Published {Success} content items ({Failed} failed)", published, failed);
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

        // Determine if this content should be published based on PublishMode
        var shouldPublish = Options.PublishMode switch
        {
            PublishMode.All => true,
            PublishMode.FirstSection => _currentSection == 1,
            PublishMode.None => false,
            _ => false
        };

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

            // Track root content for domain assignment after scopes commit
            if (isRootContent)
            {
                _rootContentForDomains.Add(content);
            }

            if (shouldPublish)
            {
                _pendingPublishContent.Add((content, true));
            }
        }
        else
        {
            // For invariant content
            SetPropertiesForCulture(content, complexity, null);
            _contentService.Save(content);

            // Track root content for domain assignment after scopes commit
            if (isRootContent)
            {
                _rootContentForDomains.Add(content);
            }

            if (shouldPublish)
            {
                _pendingPublishContent.Add((content, false));
            }
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

    /// <summary>
    /// Assigns domains for ALL cultures to root content using the v17 async API.
    /// Each root section gets one domain per language for proper multi-language routing.
    /// </summary>
    private async Task AssignAllDomainsToContentAsync(IContent content)
    {
        // Skip domain assignment if configured
        if (Options.SkipContentDomains)
        {
            Logger.LogDebug("Skipping domain assignment for '{Name}' (SkipContentDomains=true)", content.Name);
            return;
        }

        var existingDomains = (await _domainService.GetAssignedDomainsAsync(content.Key, false)).ToList();
        var existingIsoCodes = existingDomains
            .Select(d => d.LanguageIsoCode)
            .Where(c => c != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var domainSuffix = Options.DomainSuffix;
        var defaultCulture = Context.Languages.FirstOrDefault(l => l.IsDefault)?.IsoCode
            ?? Context.Languages.FirstOrDefault()?.IsoCode;

        // Build the complete domain list (existing + new for missing cultures)
        var allDomains = existingDomains
            .Select(d => new Umbraco.Cms.Core.Models.ContentEditing.DomainModel
            {
                DomainName = d.DomainName,
                IsoCode = d.LanguageIsoCode!
            })
            .ToList();

        foreach (var language in Context.Languages)
        {
            if (existingIsoCodes.Contains(language.IsoCode))
                continue;

            allDomains.Add(new Umbraco.Cms.Core.Models.ContentEditing.DomainModel
            {
                DomainName = $"{domainSuffix}/test-{content.Id}-{language.IsoCode.ToLower()}",
                IsoCode = language.IsoCode
            });
        }

        var updateModel = new Umbraco.Cms.Core.Models.ContentEditing.DomainsUpdateModel
        {
            DefaultIsoCode = defaultCulture,
            Domains = allDomains
        };

        var result = await _domainService.UpdateDomainsAsync(content.Key, updateModel);
        if (result.Success)
        {
            Logger.LogDebug("Assigned {Count} domains to root content '{Name}' (ID: {Id})",
                allDomains.Count, content.Name, content.Id);
        }
        else
        {
            Logger.LogWarning("Failed to assign domains to '{Name}': {Status}",
                content.Name, result.Status);
        }
    }

    /// <summary>
    /// Gets the culture for a property. Returns null if the property is invariant.
    /// </summary>
    private string? GetCulture(IContent content, string propertyAlias, string? culture)
    {
        if (culture == null) return null;

        var property = content.Properties.FirstOrDefault(p => p.Alias == propertyAlias);
        if (property == null) return null;

        // Check if the property type varies by culture
        return property.PropertyType.Variations.HasFlag(ContentVariation.Culture) ? culture : null;
    }

    private void SetSimpleProperties(IContent content, string? culture)
    {
        if (content.HasProperty("title"))
            content.SetValue("title", Context.Faker.Lorem.Sentence(3), GetCulture(content, "title", culture));
        if (content.HasProperty("description"))
            content.SetValue("description", Context.Faker.Lorem.Paragraph(), GetCulture(content, "description", culture));
        if (content.HasProperty("isPublished"))
            content.SetValue("isPublished", Context.Faker.Random.Bool(), GetCulture(content, "isPublished", culture));
    }

    private void SetMediumProperties(IContent content, string? culture)
    {
        if (content.HasProperty("title"))
            content.SetValue("title", Context.Faker.Lorem.Sentence(3), GetCulture(content, "title", culture));
        if (content.HasProperty("subtitle"))
            content.SetValue("subtitle", Context.Faker.Lorem.Sentence(5), GetCulture(content, "subtitle", culture));
        if (content.HasProperty("summary"))
            content.SetValue("summary", Context.Faker.Lorem.Paragraph(), GetCulture(content, "summary", culture));

        // Link to random content
        if (content.HasProperty("relatedContent") && Context.CreatedContent.Count > 0)
        {
            var randomContent = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
            content.SetValue("relatedContent", Udi.Create(Constants.UdiEntityType.Document, randomContent.Key).ToString(), GetCulture(content, "relatedContent", culture));
        }

        // Add block list content
        if (content.HasProperty("blocks"))
        {
            var blockJson = GenerateSimpleBlockListJson(content, "blocks");
            if (!string.IsNullOrEmpty(blockJson))
                content.SetValue("blocks", blockJson, GetCulture(content, "blocks", culture));
        }
    }

    private void SetComplexProperties(IContent content, string? culture)
    {
        if (content.HasProperty("title"))
            content.SetValue("title", Context.Faker.Lorem.Sentence(3), GetCulture(content, "title", culture));
        if (content.HasProperty("subtitle"))
            content.SetValue("subtitle", Context.Faker.Lorem.Sentence(5), GetCulture(content, "subtitle", culture));
        if (content.HasProperty("bodyText"))
            content.SetValue("bodyText", Context.Faker.Lorem.Paragraphs(3), GetCulture(content, "bodyText", culture));

        // Set media picker values
        if (Context.MediaItems.Count > 0)
        {
            if (content.HasProperty("mainImage"))
            {
                var randomMedia = Context.MediaItems[Context.Random.Next(Context.MediaItems.Count)];
                content.SetValue("mainImage", GenerateMediaPickerValue(randomMedia), GetCulture(content, "mainImage", culture));
            }
            if (content.HasProperty("thumbnailImage"))
            {
                var randomMedia = Context.MediaItems[Context.Random.Next(Context.MediaItems.Count)];
                content.SetValue("thumbnailImage", GenerateMediaPickerValue(randomMedia), GetCulture(content, "thumbnailImage", culture));
            }
        }

        // Link to content
        if (Context.CreatedContent.Count > 0)
        {
            if (content.HasProperty("primaryContent"))
            {
                var rc = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
                content.SetValue("primaryContent", Udi.Create(Constants.UdiEntityType.Document, rc.Key).ToString(), GetCulture(content, "primaryContent", culture));
            }
            if (content.HasProperty("secondaryContent"))
            {
                var rc = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
                content.SetValue("secondaryContent", Udi.Create(Constants.UdiEntityType.Document, rc.Key).ToString(), GetCulture(content, "secondaryContent", culture));
            }
            if (content.HasProperty("tertiaryContent"))
            {
                var rc = Context.CreatedContent[Context.Random.Next(Context.CreatedContent.Count)];
                content.SetValue("tertiaryContent", Udi.Create(Constants.UdiEntityType.Document, rc.Key).ToString(), GetCulture(content, "tertiaryContent", culture));
            }
        }

        // Add block lists
        if (content.HasProperty("headerBlocks"))
        {
            var blockJson = GenerateBlockListJsonWithAllComplexities(content, "headerBlocks");
            if (!string.IsNullOrEmpty(blockJson))
                content.SetValue("headerBlocks", blockJson, GetCulture(content, "headerBlocks", culture));
        }
        if (content.HasProperty("footerBlocks"))
        {
            var blockJson = GenerateBlockListJsonWithAllComplexities(content, "footerBlocks");
            if (!string.IsNullOrEmpty(blockJson))
                content.SetValue("footerBlocks", blockJson, GetCulture(content, "footerBlocks", culture));
        }

        // Add block grid
        if (content.HasProperty("mainGrid"))
        {
            var gridJson = GenerateBlockGridJson(content, "mainGrid");
            if (!string.IsNullOrEmpty(gridJson))
                content.SetValue("mainGrid", gridJson, GetCulture(content, "mainGrid", culture));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generates the correct JSON format for Umbraco MediaPicker3.
    /// Format: [{ "key": "guid", "mediaKey": "media-guid", "mediaTypeAlias": "Image", "crops": [], "focalPoint": null }]
    /// </summary>
    private static string GenerateMediaPickerValue(IMedia media)
    {
        // MediaPicker3 expects an array of objects with these properties
        // Format: [{"key":"guid","mediaKey":"media-guid","mediaTypeAlias":"Image","crops":[],"focalPoint":null}]
        var mediaPickerItems = new[]
        {
            new Dictionary<string, object?>
            {
                ["key"] = Guid.NewGuid().ToString(),
                ["mediaKey"] = media.Key.ToString(),
                ["mediaTypeAlias"] = media.ContentType.Alias,
                ["crops"] = Array.Empty<object>(),
                ["focalPoint"] = null
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

        var contentKey = Guid.NewGuid();
        var contentData = BuildElementContentDataObject(elementType, simpleBlock.ContentElementTypeKey, contentKey);

        var blockListValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = new[] { BuildBlockListLayoutItem(contentKey) }
            },
            contentData = new[] { contentData },
            settingsData = Array.Empty<object>(),
            expose = new[] { BuildExposeEntry(contentKey) }
        };

        return JsonSerializer.Serialize(blockListValue, JsonOptions);
    }

    private string? GenerateBlockListJsonWithAllComplexities(IContent content, string propertyAlias)
    {
        var blockConfig = GetBlockListConfiguration(content, propertyAlias);
        if (blockConfig?.Blocks == null || blockConfig.Blocks.Length == 0) return null;

        var layoutItems = new List<Dictionary<string, object?>>();
        var contentDataItems = new List<Dictionary<string, object>>();
        var exposeItems = new List<Dictionary<string, object?>>();

        // Add nested block element first (if available)
        var nestedBlock = FindNestedBlockElement(blockConfig.Blocks);
        if (nestedBlock != null)
        {
            var elementType = _contentTypeService.Get(nestedBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentKey = Guid.NewGuid();
                var nestedContentData = BuildNestedBlockContentDataObject(elementType, nestedBlock.ContentElementTypeKey, contentKey, 1);
                contentDataItems.Add(nestedContentData);
                layoutItems.Add(BuildBlockListLayoutItem(contentKey));
                exposeItems.Add(BuildExposeEntry(contentKey));
            }
        }

        // Add regular complexity blocks
        foreach (var complexity in new[] { "Simple", "Medium", "Complex" })
        {
            var block = FindBlockByComplexity(blockConfig.Blocks, complexity);
            if (block == null) continue;

            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType == null) continue;

            var contentKey = Guid.NewGuid();
            contentDataItems.Add(BuildElementContentDataObject(elementType, block.ContentElementTypeKey, contentKey));
            layoutItems.Add(BuildBlockListLayoutItem(contentKey));
            exposeItems.Add(BuildExposeEntry(contentKey));
        }

        if (layoutItems.Count == 0) return null;

        var blockListValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            contentData = contentDataItems,
            settingsData = Array.Empty<object>(),
            expose = exposeItems
        };

        return JsonSerializer.Serialize(blockListValue, JsonOptions);
    }

    private string? GenerateBlockGridJson(IContent content, string propertyAlias)
    {
        var blockConfig = GetBlockGridConfiguration(content, propertyAlias);
        if (blockConfig?.Blocks == null || blockConfig.Blocks.Length == 0) return null;

        var layoutItems = new List<Dictionary<string, object?>>();
        var contentDataItems = new List<Dictionary<string, object>>();
        var exposeItems = new List<Dictionary<string, object?>>();

        // Add nested block element first (if available)
        var nestedBlock = FindNestedBlockGridElement(blockConfig.Blocks);
        if (nestedBlock != null)
        {
            var elementType = _contentTypeService.Get(nestedBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentKey = Guid.NewGuid();
                var nestedContentData = BuildNestedBlockContentDataObject(elementType, nestedBlock.ContentElementTypeKey, contentKey, 1);
                contentDataItems.Add(nestedContentData);
                layoutItems.Add(BuildBlockGridLayoutItem(contentKey));
                exposeItems.Add(BuildExposeEntry(contentKey));
            }
        }

        // Add regular complexity blocks
        foreach (var complexity in new[] { "Simple", "Medium", "Complex" })
        {
            var block = FindBlockGridByComplexity(blockConfig.Blocks, complexity);
            if (block == null) continue;

            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType == null) continue;

            var contentKey = Guid.NewGuid();
            contentDataItems.Add(BuildElementContentDataObject(elementType, block.ContentElementTypeKey, contentKey));
            layoutItems.Add(BuildBlockGridLayoutItem(contentKey));
            exposeItems.Add(BuildExposeEntry(contentKey));
        }

        if (layoutItems.Count == 0) return null;

        var blockGridValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockGrid"] = layoutItems
            },
            contentData = contentDataItems,
            settingsData = Array.Empty<object>(),
            expose = exposeItems
        };

        return JsonSerializer.Serialize(blockGridValue, JsonOptions);
    }

    /// <summary>
    /// Builds content data for a block element using the v17 format.
    /// Uses key-based references and values array instead of flat properties.
    /// </summary>
    private Dictionary<string, object> BuildElementContentDataObject(IContentType elementType, Guid elementTypeKey, Guid contentKey)
    {
        var values = BuildBlockValuesArray(elementType);

        return new Dictionary<string, object>
        {
            ["key"] = contentKey,
            ["contentTypeKey"] = elementTypeKey,
            ["values"] = values
        };
    }

    /// <summary>
    /// Builds the values array for a block element's properties in v17 format.
    /// Each entry contains editorAlias, alias, culture, segment, and value.
    /// </summary>
    private List<Dictionary<string, object?>> BuildBlockValuesArray(IContentType elementType)
    {
        var values = new List<Dictionary<string, object?>>();

        foreach (var group in elementType.PropertyGroups)
        {
            if (group.PropertyTypes == null) continue;
            foreach (var propType in group.PropertyTypes)
            {
                var (editorAlias, propValue) = GenerateBlockPropertyValue(propType);
                if (editorAlias != null && propValue != null)
                {
                    values.Add(new Dictionary<string, object?>
                    {
                        ["editorAlias"] = editorAlias,
                        ["alias"] = propType.Alias,
                        ["culture"] = null,
                        ["segment"] = null,
                        ["value"] = propValue
                    });
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Builds content data for nested block elements, recursively generating child blocks.
    /// Uses configured NestingDepth from DocumentTypes configuration.
    /// Uses v17 format with key-based references and values array.
    /// </summary>
    private Dictionary<string, object> BuildNestedBlockContentDataObject(
        IContentType elementType, Guid elementTypeKey, Guid contentKey, int currentDepth, int? maxDepth = null)
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
                ["key"] = contentKey,
                ["contentTypeKey"] = elementTypeKey,
                ["values"] = new List<Dictionary<string, object?>>()
            };
        }

        var values = new List<Dictionary<string, object?>>();

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
                    {
                        values.Add(new Dictionary<string, object?>
                        {
                            ["editorAlias"] = Constants.PropertyEditors.Aliases.BlockList,
                            ["alias"] = propType.Alias,
                            ["culture"] = null,
                            ["segment"] = null,
                            ["value"] = nestedBlockListJson
                        });
                    }
                }
                else
                {
                    var (editorAlias, propValue) = GenerateBlockPropertyValue(propType);
                    if (editorAlias != null && propValue != null)
                    {
                        values.Add(new Dictionary<string, object?>
                        {
                            ["editorAlias"] = editorAlias,
                            ["alias"] = propType.Alias,
                            ["culture"] = null,
                            ["segment"] = null,
                            ["value"] = propValue
                        });
                    }
                }
            }
        }

        return new Dictionary<string, object>
        {
            ["key"] = contentKey,
            ["contentTypeKey"] = elementTypeKey,
            ["values"] = values
        };
    }

    /// <summary>
    /// Generates BlockList JSON for nested blocks property using v17 format.
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
        // Data types are pre-loaded in PreloadDataTypeCacheAsync
        Context.DataTypeCache.TryGetValue(propType.DataTypeId, out var dataType);

        if (dataType?.ConfigurationObject is not BlockListConfiguration blockConfig)
            return null;

        if (blockConfig.Blocks == null || blockConfig.Blocks.Length == 0)
            return null;

        var layoutItems = new List<Dictionary<string, object?>>();
        var contentDataItems = new List<Dictionary<string, object>>();
        var exposeItems = new List<Dictionary<string, object?>>();

        // Check if there are more nested containers at this level
        var nestedBlock = FindNestedBlockElement(blockConfig.Blocks);
        if (nestedBlock != null && currentDepth < maxDepth)
        {
            var elementType = _contentTypeService.Get(nestedBlock.ContentElementTypeKey);
            if (elementType != null)
            {
                var contentKey = Guid.NewGuid();
                var nestedContentData = BuildNestedBlockContentDataObject(
                    elementType, nestedBlock.ContentElementTypeKey, contentKey, currentDepth, maxDepth);
                contentDataItems.Add(nestedContentData);
                layoutItems.Add(BuildBlockListLayoutItem(contentKey));
                exposeItems.Add(BuildExposeEntry(contentKey));
            }
        }

        // Add leaf elements (Simple/Medium/Complex)
        foreach (var complexity in new[] { "Simple", "Medium" })
        {
            var block = FindBlockByComplexity(blockConfig.Blocks, complexity);
            if (block == null) continue;

            var elementType = _contentTypeService.Get(block.ContentElementTypeKey);
            if (elementType == null) continue;

            var contentKey = Guid.NewGuid();
            contentDataItems.Add(BuildElementContentDataObject(elementType, block.ContentElementTypeKey, contentKey));
            layoutItems.Add(BuildBlockListLayoutItem(contentKey));
            exposeItems.Add(BuildExposeEntry(contentKey));
        }

        if (layoutItems.Count == 0) return null;

        var blockListValue = new
        {
            layout = new Dictionary<string, object>
            {
                ["Umbraco.BlockList"] = layoutItems
            },
            contentData = contentDataItems,
            settingsData = Array.Empty<object>(),
            expose = exposeItems
        };

        return JsonSerializer.Serialize(blockListValue, JsonOptions);
    }

    /// <summary>
    /// Generates a block property value along with its editor alias for the v17 block value format.
    /// Returns (editorAlias, value) tuple where editorAlias is needed for the values array.
    /// </summary>
    private (string? editorAlias, object? value) GenerateBlockPropertyValue(IPropertyType propType)
    {
        // Use cached data types if available
        // Data types are pre-loaded in PreloadDataTypeCacheAsync
        Context.DataTypeCache.TryGetValue(propType.DataTypeId, out var dataType);

        if (dataType == null) return (null, null);

        object? value = dataType.EditorAlias switch
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

        return (dataType.EditorAlias, value);
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

        // Data types are pre-loaded in PreloadDataTypeCacheAsync
        Context.DataTypeCache.TryGetValue(propertyType.DataTypeId, out var dataType);

        var config = dataType?.ConfigurationObject as BlockListConfiguration;
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

        // Data types are pre-loaded in PreloadDataTypeCacheAsync
        Context.DataTypeCache.TryGetValue(propertyType.DataTypeId, out var dataType);

        var config = dataType?.ConfigurationObject as BlockGridConfiguration;
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

    #region Block Value Format Helpers (v17)

    /// <summary>
    /// Builds a BlockList layout item in v17 format using contentKey instead of contentUdi.
    /// </summary>
    private static Dictionary<string, object?> BuildBlockListLayoutItem(Guid contentKey) => new()
    {
        ["contentKey"] = contentKey,
        ["settingsKey"] = null
    };

    /// <summary>
    /// Builds a BlockGrid layout item in v17 format with columnSpan, rowSpan, and areas.
    /// </summary>
    private Dictionary<string, object?> BuildBlockGridLayoutItem(Guid contentKey) => new()
    {
        ["contentKey"] = contentKey,
        ["settingsKey"] = null,
        ["columnSpan"] = DefaultGridColumnSpan,
        ["rowSpan"] = DefaultGridRowSpan,
        ["areas"] = Array.Empty<object>()
    };

    /// <summary>
    /// Builds an expose entry for invariant block content in v17 format.
    /// Each block must have an expose entry to be visible.
    /// </summary>
    private static Dictionary<string, object?> BuildExposeEntry(Guid contentKey) => new()
    {
        ["contentKey"] = contentKey,
        ["culture"] = null,
        ["segment"] = null
    };

    #endregion

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
