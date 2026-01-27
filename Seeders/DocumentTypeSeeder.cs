namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Seeds element types, document types, block data types, and templates.
/// Execution order: 4 (depends on DataTypeSeeder).
/// This is a partial class split across multiple files for maintainability:
/// - DocumentTypeSeeder.cs (this file): Core orchestration and helpers
/// - DocumentTypeSeeder.ElementTypes.cs: Element type creation
/// - DocumentTypeSeeder.BlockList.cs: Block List data type creation
/// - DocumentTypeSeeder.BlockGrid.cs: Block Grid data type creation
/// - DocumentTypeSeeder.DocTypes.cs: Document type and template creation
/// </summary>
public partial class DocumentTypeSeeder : BaseSeeder<DocumentTypeSeeder>
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IFileService _fileService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;
    private readonly IShortStringHelper _shortStringHelper;

    // Cached element types categorized by complexity (for Block List/Grid creation)
    private List<IContentType>? _cachedSimpleElements;
    private List<IContentType>? _cachedMediumElements;
    private List<IContentType>? _cachedComplexElements;

    // Nested container elements by level (level 1 = top, higher = deeper)
    private Dictionary<int, List<IContentType>> _nestedContainersByLevel = new();

    /// <summary>
    /// Creates a new DocumentTypeSeeder instance.
    /// </summary>
    public DocumentTypeSeeder(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IFileService fileService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer,
        IShortStringHelper shortStringHelper,
        IScopeProvider scopeProvider,
        ILogger<DocumentTypeSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _fileService = fileService;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
        _shortStringHelper = shortStringHelper;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 4;

    /// <inheritdoc />
    public override string SeederName => "DocumentTypeSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.DocumentTypes;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var elementPrefix = GetPrefix(PrefixType.ElementType);
        var variantPrefix = GetPrefix(PrefixType.VariantDocType);
        var existingTypes = _contentTypeService.GetAll();
        return existingTypes.Any(t =>
            t.Alias.StartsWith(elementPrefix, StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith(variantPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        var docTypeConfig = Config.DocumentTypes;

        if (IsDryRun)
        {
            var totalElements = docTypeConfig.ElementTypes.Simple + docTypeConfig.ElementTypes.Medium + docTypeConfig.ElementTypes.Complex;
            var totalVariantDocs = docTypeConfig.VariantDocTypes.Simple + docTypeConfig.VariantDocTypes.Medium + docTypeConfig.VariantDocTypes.Complex;
            var totalInvariantDocs = docTypeConfig.InvariantDocTypes.Simple + docTypeConfig.InvariantDocTypes.Medium + docTypeConfig.InvariantDocTypes.Complex;
            Logger.LogInformation("[DRY-RUN] Would create Document Types with configuration:");
            Logger.LogInformation("[DRY-RUN]   Element Types: {Simple} simple, {Medium} medium, {Complex} complex (total: {Total})",
                docTypeConfig.ElementTypes.Simple, docTypeConfig.ElementTypes.Medium, docTypeConfig.ElementTypes.Complex, totalElements);
            Logger.LogInformation("[DRY-RUN]   Nesting Depth: {Depth}", docTypeConfig.NestingDepth);
            Logger.LogInformation("[DRY-RUN]   Block List: {Count} data types", docTypeConfig.BlockList);
            Logger.LogInformation("[DRY-RUN]   Block Grid: {Count} data types", docTypeConfig.BlockGrid);
            Logger.LogInformation("[DRY-RUN]   Variant Doc Types: {Simple} simple, {Medium} medium, {Complex} complex (total: {Total})",
                docTypeConfig.VariantDocTypes.Simple, docTypeConfig.VariantDocTypes.Medium, docTypeConfig.VariantDocTypes.Complex, totalVariantDocs);
            Logger.LogInformation("[DRY-RUN]   Invariant Doc Types: {Simple} simple, {Medium} medium, {Complex} complex (total: {Total})",
                docTypeConfig.InvariantDocTypes.Simple, docTypeConfig.InvariantDocTypes.Medium, docTypeConfig.InvariantDocTypes.Complex, totalInvariantDocs);
            // Still load built-in data types for validation
            EnsureBuiltInDataTypesLoaded();
            return Task.CompletedTask;
        }

        // Ensure built-in data types are loaded
        EnsureBuiltInDataTypesLoaded();

        List<IDataType> blockListDataTypes;
        List<IDataType> blockGridDataTypes;

        // Phase 1: Create Element Types (needed for Block List/Grid)
        Logger.LogInformation("Phase 1: Creating Element Types...");
        using (var scope = CreateScopedBatch())
        {
            CreateElementTypes(docTypeConfig.ElementTypes, cancellationToken);
            scope.Complete();
        }

        // Cache element types by complexity for Block List/Grid creation
        CategorizeElementTypes();

        // Phase 2: Create Nested Container Elements (if NestingDepth > 1)
        if (docTypeConfig.NestingDepth > 1)
        {
            Logger.LogInformation("Phase 2: Creating Nested Container Elements (depth: {Depth})...", docTypeConfig.NestingDepth);
            using (var scope = CreateScopedBatch())
            {
                CreateNestedContainerElements(docTypeConfig.NestingDepth, cancellationToken);
                scope.Complete();
            }
        }

        // Phase 3: Create Block List data types
        Logger.LogInformation("Phase 3: Creating Block List data types...");
        using (var scope = CreateScopedBatch())
        {
            blockListDataTypes = CreateBlockListDataTypes(docTypeConfig.BlockList, cancellationToken);
            scope.Complete();
        }

        // Phase 4: Create Block Grid data types
        Logger.LogInformation("Phase 4: Creating Block Grid data types...");
        using (var scope = CreateScopedBatch())
        {
            blockGridDataTypes = CreateBlockGridDataTypes(docTypeConfig.BlockGrid, cancellationToken);
            scope.Complete();
        }

        // Phase 5: Create Variant Document Types with Templates
        Logger.LogInformation("Phase 5: Creating Variant Document Types with Templates...");
        using (var scope = CreateScopedBatch())
        {
            CreateVariantDocumentTypes(docTypeConfig.VariantDocTypes, blockListDataTypes, blockGridDataTypes, cancellationToken);
            scope.Complete();
        }

        // Phase 6: Create Invariant Document Types with Templates
        Logger.LogInformation("Phase 6: Creating Invariant Document Types with Templates...");
        using (var scope = CreateScopedBatch())
        {
            CreateInvariantDocumentTypes(docTypeConfig.InvariantDocTypes, blockListDataTypes, blockGridDataTypes, cancellationToken);
            scope.Complete();
        }

        // Store block data types in context for ContentSeeder
        Context.AddBlockListDataTypes(blockListDataTypes);
        Context.AddBlockGridDataTypes(blockGridDataTypes);

        Logger.LogInformation(
            "Seeded {ElementCount} Element Types, {BlockListCount} Block List, {BlockGridCount} Block Grid, and {DocTypeCount} Document Types",
            Context.ElementTypes.Count,
            blockListDataTypes.Count,
            blockGridDataTypes.Count,
            docTypeConfig.TotalDocTypes);

        return Task.CompletedTask;
    }

    #region Data Type Helpers

    private void EnsureBuiltInDataTypesLoaded()
    {
        if (Context.TextstringDataType == null)
        {
            Context.TextstringDataType = _dataTypeService.GetDataType(Constants.DataTypes.Textbox);
            Context.TextareaDataType = _dataTypeService.GetDataType(Constants.DataTypes.Textarea);
            Context.TrueFalseDataType = _dataTypeService.GetDataType(Constants.DataTypes.Boolean);
            Context.LabelDataType = _dataTypeService.GetDataType(Constants.DataTypes.LabelString);
            Context.UploadDataType = _dataTypeService.GetDataType(Constants.DataTypes.Upload);

            var allDataTypes = _dataTypeService.GetAll().ToList();
            Context.ContentPickerDataType = allDataTypes.FirstOrDefault(d =>
                d.EditorAlias == Constants.PropertyEditors.Aliases.ContentPicker);
            Context.MediaPickerDataType = allDataTypes.FirstOrDefault(d =>
                d.EditorAlias == Constants.PropertyEditors.Aliases.MediaPicker3);
        }

        // Validate required data types are available
        ValidateRequiredDataTypes();
    }

    /// <summary>
    /// Validates that all required built-in data types are loaded.
    /// Throws InvalidOperationException if any required type is missing.
    /// Logs warnings for optional types that are missing.
    /// </summary>
    private void ValidateRequiredDataTypes()
    {
        var missing = new List<string>();

        if (Context.TextstringDataType == null) missing.Add("Textstring");
        if (Context.TextareaDataType == null) missing.Add("Textarea");
        if (Context.TrueFalseDataType == null) missing.Add("True/False");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Required Umbraco data types not found: {string.Join(", ", missing)}. " +
                "Ensure Umbraco is fully installed before running the seeder.");
        }

        // Log warnings for optional data types that are missing
        // These will cause some properties to be skipped but won't break seeding
        var optionalMissing = new List<string>();
        if (Context.ContentPickerDataType == null)
        {
            optionalMissing.Add("ContentPicker");
            Logger.LogWarning("ContentPicker data type not found. Content picker properties will be skipped on element types.");
        }
        if (Context.MediaPickerDataType == null)
        {
            optionalMissing.Add("MediaPicker3");
            Logger.LogWarning("MediaPicker3 data type not found. Media picker properties will be skipped on element types.");
        }
        if (Context.LabelDataType == null)
        {
            optionalMissing.Add("Label");
            Logger.LogDebug("Label data type not found. Label properties will be skipped.");
        }
    }

    /// <summary>
    /// Gets the Textstring data type. Throws if not loaded.
    /// </summary>
    private IDataType GetTextstringDataType() =>
        Context.TextstringDataType ?? throw new InvalidOperationException("Textstring data type not loaded");

    /// <summary>
    /// Gets the Textarea data type. Throws if not loaded.
    /// </summary>
    private IDataType GetTextareaDataType() =>
        Context.TextareaDataType ?? throw new InvalidOperationException("Textarea data type not loaded");

    /// <summary>
    /// Gets the True/False data type. Throws if not loaded.
    /// </summary>
    private IDataType GetTrueFalseDataType() =>
        Context.TrueFalseDataType ?? throw new InvalidOperationException("True/False data type not loaded");

    #endregion

    #region Element Type Categorization

    private void CategorizeElementTypes()
    {
        // Use precise matching to avoid false positives (e.g., "VerySimple" matching "Simple")
        // Element types are created with aliases like: {prefix}Simple{N}, {prefix}Medium{N}, {prefix}Complex{N}
        // Also exclude nested container elements which have "Container" in the name
        _cachedSimpleElements = Context.ElementTypes
            .Where(e => IsComplexityMatch(e.Alias, "Simple") && !e.Alias.Contains("Container"))
            .ToList();
        _cachedMediumElements = Context.ElementTypes
            .Where(e => IsComplexityMatch(e.Alias, "Medium") && !e.Alias.Contains("Container"))
            .ToList();
        _cachedComplexElements = Context.ElementTypes
            .Where(e => IsComplexityMatch(e.Alias, "Complex") && !e.Alias.Contains("Container"))
            .ToList();

        Logger.LogDebug("Categorized elements - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}",
            _cachedSimpleElements.Count, _cachedMediumElements.Count, _cachedComplexElements.Count);
    }

    /// <summary>
    /// Checks if an alias matches a complexity level using precise matching.
    /// Matches patterns like "testElementSimple1" or "prefixSimple_2".
    /// </summary>
    private static bool IsComplexityMatch(string alias, string complexity)
    {
        // Match: alias contains complexity followed by a digit or underscore+digit or end of string
        // e.g., "testElementSimple1" matches "Simple", "prefix_Simple_1" matches "Simple"
        var index = alias.IndexOf(complexity, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return false;

        var afterComplexity = index + complexity.Length;
        if (afterComplexity >= alias.Length) return true; // Ends with complexity

        var nextChar = alias[afterComplexity];
        // Valid if followed by digit, underscore, or nothing
        return char.IsDigit(nextChar) || nextChar == '_';
    }

    #endregion
}
