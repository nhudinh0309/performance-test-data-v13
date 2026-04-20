namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Seeds custom data types for use by document types.
/// Execution order: 3 (depends on LanguageSeeder, DictionarySeeder).
/// </summary>
public class DataTypeSeeder : BaseSeeder<DataTypeSeeder>
{
    private readonly IDataTypeService _dataTypeService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;
    private readonly IDataTypeContainerService _dataTypeContainerService;

    private int _testDataTypesFolderId = -1;

    /// <summary>
    /// Creates a new DataTypeSeeder instance.
    /// </summary>
    public DataTypeSeeder(
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer,
        IDataTypeContainerService dataTypeContainerService,
        IScopeProvider scopeProvider,
        ILogger<DataTypeSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
    {
        _dataTypeService = dataTypeService;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
        _dataTypeContainerService = dataTypeContainerService;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 3;

    /// <inheritdoc />
    public override string SeederName => "DataTypeSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.DataTypes;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var prefix = GetPrefix(PrefixType.DataType);
        var existing = _dataTypeService.GetAllAsync(Array.Empty<Guid>()).GetAwaiter().GetResult();
        return existing.Any(d => d.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var dataTypesConfig = Config.DataTypes;
        var prefix = GetPrefix(PrefixType.DataType);
        int totalTarget = dataTypesConfig.Total;

        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create {Total} data types with prefix '{Prefix}'", totalTarget, prefix);
            Logger.LogInformation("[DRY-RUN] Types: ListView={ListView}, MNTP={MNTP}, RTE={RTE}, MediaPicker={Media}, Textarea={Textarea}, Dropdown={Dropdown}, Numeric={Numeric}",
                dataTypesConfig.ListView, dataTypesConfig.MultiNodeTreePicker, dataTypesConfig.RichTextEditor,
                dataTypesConfig.MediaPicker, dataTypesConfig.Textarea, dataTypesConfig.Dropdown, dataTypesConfig.Numeric);
            // Still load built-in data types for DryRun validation
            await LoadBuiltInDataTypesAsync();
            return;
        }

        int totalCreated = 0;

        // Create data type folders: Test Data Types → Block List / Block Grid
        await CreateDataTypeFoldersAsync();

        // Use a single scope for all data type creation (usually small numbers)
        using (var scope = CreateScopedBatch())
        {
            // Create each type of data type
            totalCreated += await CreateListViewDataTypes(prefix, dataTypesConfig.ListView, cancellationToken);
            totalCreated += await CreateMultiNodeTreePickerDataTypes(prefix, dataTypesConfig.MultiNodeTreePicker, cancellationToken);
            totalCreated += await CreateRichTextEditorDataTypes(prefix, dataTypesConfig.RichTextEditor, cancellationToken);
            totalCreated += await CreateMediaPickerDataTypes(prefix, dataTypesConfig.MediaPicker, cancellationToken);
            totalCreated += await CreateTextareaDataTypes(prefix, dataTypesConfig.Textarea, cancellationToken);
            totalCreated += await CreateDropdownDataTypes(prefix, dataTypesConfig.Dropdown, cancellationToken);
            totalCreated += await CreateNumericDataTypes(prefix, dataTypesConfig.Numeric, cancellationToken);

            scope.Complete();
        }

        // Load and cache built-in data types for other seeders
        await LoadBuiltInDataTypesAsync();

        Logger.LogInformation(
            "Seeded {Created} data types (target: {Target}). Block List/Grid will be created after Element Types.",
            totalCreated, totalTarget);
    }

    private async Task LoadBuiltInDataTypesAsync()
    {
        // Load all data types in one async call instead of individual sync lookups
        var allDataTypes = (await _dataTypeService.GetAllAsync(Array.Empty<Guid>())).ToList();

        Context.TextstringDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.Textbox);
        Context.TextareaDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.Textarea);
        Context.TrueFalseDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.Boolean);
        Context.LabelDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.LabelString);
        Context.UploadDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.Upload);
        Context.DateTimeDataType = allDataTypes.FirstOrDefault(d => d.Id == Constants.DataTypes.DateTime);

        // Get data types by editor alias
        Context.NumericDataType = allDataTypes.FirstOrDefault(d =>
            d.EditorAlias == Constants.PropertyEditors.Aliases.Integer);
        Context.ContentPickerDataType = allDataTypes.FirstOrDefault(d =>
            d.EditorAlias == Constants.PropertyEditors.Aliases.ContentPicker);
        Context.MediaPickerDataType = allDataTypes.FirstOrDefault(d =>
            d.EditorAlias == Constants.PropertyEditors.Aliases.MediaPicker3);

        // Build data type cache
        foreach (var dt in allDataTypes)
        {
            Context.DataTypeCache[dt.Id] = dt;
        }

        Logger.LogDebug("Cached {Count} data types for other seeders", allDataTypes.Count);
    }

    private async Task CreateDataTypeFoldersAsync()
    {
        var userKey = Constants.Security.SuperUserKey;

        // Root folder
        var rootResult = await _dataTypeContainerService.CreateAsync(null, "Test Data Types", null, userKey);
        if (rootResult.Success && rootResult.Result is not null)
        {
            _testDataTypesFolderId = rootResult.Result.Id;
            Context.TestDataTypesFolderId = _testDataTypesFolderId;

            // Child folders under "Test Data Types"
            var parentKey = rootResult.Result.Key;

            var blResult = await _dataTypeContainerService.CreateAsync(parentKey, "Block List", null, userKey);
            if (blResult.Success && blResult.Result is not null)
                Context.BlockListFolderId = blResult.Result.Id;

            var bgResult = await _dataTypeContainerService.CreateAsync(parentKey, "Block Grid", null, userKey);
            if (bgResult.Success && bgResult.Result is not null)
                Context.BlockGridFolderId = bgResult.Result.Id;

            Logger.LogInformation("Created data type folders: Test Data Types → Block List, Block Grid");
        }
        else
        {
            Logger.LogWarning("Failed to create Test Data Types folder: {Status}", rootResult.Status);
        }
    }

    /// <summary>
    /// Converts a configuration object to ConfigurationData dictionary using the editor's configuration editor.
    /// </summary>
    private IDictionary<string, object> ToConfigurationData(IDataEditor editor, object configObject)
    {
        return editor.GetConfigurationEditor().FromConfigurationObject(configObject, _serializer);
    }

    private async Task<int> CreateListViewDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.ListView];
        if (editor == null)
        {
            Logger.LogWarning("ListView property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}ListView_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Nvarchar,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "ListView data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create ListView data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }

    private async Task<int> CreateMultiNodeTreePickerDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.MultiNodeTreePicker];
        if (editor == null)
        {
            Logger.LogWarning("MultiNodeTreePicker property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}MNTP_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Ntext,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                var config = new MultiNodePickerConfiguration
                {
                    TreeSource = new MultiNodePickerConfigurationTreeSource
                    {
                        ObjectType = "content"
                    },
                    MaxNumber = i % 5 + 1,
                    MinNumber = 0,
                    IgnoreUserStartNodes = false
                };
                dataType.ConfigurationData = ToConfigurationData(editor, config);

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "MNTP data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create MNTP data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }

    private async Task<int> CreateRichTextEditorDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.RichText];
        if (editor == null)
        {
            Logger.LogWarning("RichText property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}RTE_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Ntext,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "RTE data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create RTE data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }

    private async Task<int> CreateMediaPickerDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.MediaPicker3];
        if (editor == null)
        {
            Logger.LogWarning("MediaPicker3 property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}MediaPicker_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Ntext,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "MediaPicker data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create MediaPicker data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }

    private async Task<int> CreateTextareaDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.TextArea];
        if (editor == null)
        {
            Logger.LogWarning("TextArea property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}Textarea_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Ntext,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "Textarea data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Textarea data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }

    private async Task<int> CreateDropdownDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.DropDownListFlexible];
        if (editor == null)
        {
            Logger.LogWarning("DropDownListFlexible property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}Dropdown_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Nvarchar,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                var config = new DropDownFlexibleConfiguration
                {
                    Items = new List<string>
                    {
                        "Option 1",
                        "Option 2",
                        "Option 3",
                        "Option 4",
                        "Option 5"
                    },
                    Multiple = i % 2 == 0
                };
                dataType.ConfigurationData = ToConfigurationData(editor, config);

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "Dropdown data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Dropdown data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }

    private async Task<int> CreateNumericDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.Integer];
        if (editor == null)
        {
            Logger.LogWarning("Integer property editor not found");
            return 0;
        }

        var userKey = Constants.Security.SuperUserKey;
        int created = 0;
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}Numeric_{i}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    ParentId = _testDataTypesFolderId,
                    DatabaseType = ValueStorageType.Integer,
                    EditorUiAlias = SeederConstants.GetEditorUiAlias(editor.Alias)
                };

                await _dataTypeService.CreateAsync(dataType, userKey);
                created++;
                LogProgress(created, count, "Numeric data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Numeric data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        return created;
    }
}
