namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
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
        ILogger<DocumentTypeSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context)
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
        var elementPrefix = GetPrefix("elementtype");
        var variantPrefix = GetPrefix("variantdoctype");
        var existingTypes = _contentTypeService.GetAll();
        return existingTypes.Any(t =>
            t.Alias.StartsWith(elementPrefix, StringComparison.OrdinalIgnoreCase) ||
            t.Alias.StartsWith(variantPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        // Ensure built-in data types are loaded
        EnsureBuiltInDataTypesLoaded();

        var docTypeConfig = Config.DocumentTypes;

        // Phase 1: Create Element Types (needed for Block List/Grid)
        Logger.LogInformation("Phase 1: Creating Element Types...");
        CreateElementTypes(docTypeConfig.ElementTypes, cancellationToken);

        // Cache element types by complexity for Block List/Grid creation
        CategorizeElementTypes();

        // Phase 2: Create Nested Container Elements (if NestingDepth > 1)
        if (docTypeConfig.NestingDepth > 1)
        {
            Logger.LogInformation("Phase 2: Creating Nested Container Elements (depth: {Depth})...", docTypeConfig.NestingDepth);
            CreateNestedContainerElements(docTypeConfig.NestingDepth, cancellationToken);
        }

        // Phase 3: Create Block List data types
        Logger.LogInformation("Phase 3: Creating Block List data types...");
        var blockListDataTypes = CreateBlockListDataTypes(docTypeConfig.BlockList, cancellationToken);

        // Phase 4: Create Block Grid data types
        Logger.LogInformation("Phase 4: Creating Block Grid data types...");
        var blockGridDataTypes = CreateBlockGridDataTypes(docTypeConfig.BlockGrid, cancellationToken);

        // Phase 5: Create Variant Document Types with Templates
        Logger.LogInformation("Phase 5: Creating Variant Document Types with Templates...");
        CreateVariantDocumentTypes(docTypeConfig.VariantDocTypes, blockListDataTypes, blockGridDataTypes, cancellationToken);

        // Phase 6: Create Invariant Document Types with Templates
        Logger.LogInformation("Phase 6: Creating Invariant Document Types with Templates...");
        CreateInvariantDocumentTypes(docTypeConfig.InvariantDocTypes, blockListDataTypes, blockGridDataTypes, cancellationToken);

        // Store block data types in context for ContentSeeder
        Context.BlockListDataTypes.AddRange(blockListDataTypes);
        Context.BlockGridDataTypes.AddRange(blockGridDataTypes);

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
        _cachedSimpleElements = Context.ElementTypes.Where(e => e.Alias.Contains("Simple")).ToList();
        _cachedMediumElements = Context.ElementTypes.Where(e => e.Alias.Contains("Medium")).ToList();
        _cachedComplexElements = Context.ElementTypes.Where(e => e.Alias.Contains("Complex")).ToList();

        Logger.LogDebug("Categorized elements - Simple: {Simple}, Medium: {Medium}, Complex: {Complex}",
            _cachedSimpleElements.Count, _cachedMediumElements.Count, _cachedComplexElements.Count);
    }

    #endregion
}
