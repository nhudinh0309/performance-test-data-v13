namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
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

    /// <summary>
    /// Creates a new DataTypeSeeder instance.
    /// </summary>
    public DataTypeSeeder(
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer,
        ILogger<DataTypeSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context)
    {
        _dataTypeService = dataTypeService;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
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
        var prefix = GetPrefix("datatype");
        var existing = _dataTypeService.GetAll();
        return existing.Any(d => d.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        var dataTypesConfig = Config.DataTypes;
        var prefix = GetPrefix("datatype");
        int totalCreated = 0;
        int totalTarget = dataTypesConfig.Total;

        // Create each type of data type
        totalCreated += CreateListViewDataTypes(prefix, dataTypesConfig.ListView, cancellationToken);
        totalCreated += CreateMultiNodeTreePickerDataTypes(prefix, dataTypesConfig.MultiNodeTreePicker, cancellationToken);
        totalCreated += CreateRichTextEditorDataTypes(prefix, dataTypesConfig.RichTextEditor, cancellationToken);
        totalCreated += CreateMediaPickerDataTypes(prefix, dataTypesConfig.MediaPicker, cancellationToken);
        totalCreated += CreateTextareaDataTypes(prefix, dataTypesConfig.Textarea, cancellationToken);
        totalCreated += CreateDropdownDataTypes(prefix, dataTypesConfig.Dropdown, cancellationToken);
        totalCreated += CreateNumericDataTypes(prefix, dataTypesConfig.Numeric, cancellationToken);

        // Load and cache built-in data types for other seeders
        LoadBuiltInDataTypes();

        Logger.LogInformation(
            "Seeded {Created} data types (target: {Target}). Block List/Grid will be created after Element Types.",
            totalCreated, totalTarget);

        return Task.CompletedTask;
    }

    private void LoadBuiltInDataTypes()
    {
        Context.TextstringDataType = _dataTypeService.GetDataType(Constants.DataTypes.Textbox);
        Context.TextareaDataType = _dataTypeService.GetDataType(Constants.DataTypes.Textarea);
        Context.TrueFalseDataType = _dataTypeService.GetDataType(Constants.DataTypes.Boolean);
        Context.LabelDataType = _dataTypeService.GetDataType(Constants.DataTypes.LabelString);
        Context.UploadDataType = _dataTypeService.GetDataType(Constants.DataTypes.Upload);
        Context.DateTimeDataType = _dataTypeService.GetDataType(Constants.DataTypes.DateTime);

        // Get data types by editor alias
        var allDataTypes = _dataTypeService.GetAll().ToList();
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

    private int CreateListViewDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.ListView];
        if (editor == null)
        {
            Logger.LogWarning("ListView property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Nvarchar
                };

                _dataTypeService.Save(dataType);
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

    private int CreateMultiNodeTreePickerDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.MultiNodeTreePicker];
        if (editor == null)
        {
            Logger.LogWarning("MultiNodeTreePicker property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Ntext
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
                dataType.Configuration = config;

                _dataTypeService.Save(dataType);
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

    private int CreateRichTextEditorDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.TinyMce];
        if (editor == null)
        {
            Logger.LogWarning("TinyMce property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Ntext
                };

                _dataTypeService.Save(dataType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create RTE data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} RTE data types", created);
        return created;
    }

    private int CreateMediaPickerDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.MediaPicker3];
        if (editor == null)
        {
            Logger.LogWarning("MediaPicker3 property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Ntext
                };

                _dataTypeService.Save(dataType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create MediaPicker data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} MediaPicker data types", created);
        return created;
    }

    private int CreateTextareaDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.TextArea];
        if (editor == null)
        {
            Logger.LogWarning("TextArea property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Ntext
                };

                _dataTypeService.Save(dataType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Textarea data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} Textarea data types", created);
        return created;
    }

    private int CreateDropdownDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.DropDownListFlexible];
        if (editor == null)
        {
            Logger.LogWarning("DropDownListFlexible property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Nvarchar
                };

                var config = new DropDownFlexibleConfiguration
                {
                    Items = new List<ValueListConfiguration.ValueListItem>
                    {
                        new() { Id = 1, Value = "Option 1" },
                        new() { Id = 2, Value = "Option 2" },
                        new() { Id = 3, Value = "Option 3" },
                        new() { Id = 4, Value = "Option 4" },
                        new() { Id = 5, Value = "Option 5" }
                    },
                    Multiple = i % 2 == 0
                };
                dataType.Configuration = config;

                _dataTypeService.Save(dataType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Dropdown data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} Dropdown data types", created);
        return created;
    }

    private int CreateNumericDataTypes(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return 0;

        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.Integer];
        if (editor == null)
        {
            Logger.LogWarning("Integer property editor not found");
            return 0;
        }

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
                    DatabaseType = ValueStorageType.Integer
                };

                _dataTypeService.Save(dataType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Numeric data type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} Numeric data types", created);
        return created;
    }
}
