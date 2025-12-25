using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;

public class DataTypeSeeder : IHostedService
{
    private readonly IDataTypeService _dataTypeService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    public DataTypeSeeder(
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config)
    {
        _dataTypeService = dataTypeService;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
        _runtimeState = runtimeState;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed when Umbraco is fully installed and running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            Console.WriteLine("DataTypeSeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

        SeedDataTypes();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedDataTypes()
    {
        // Check if already seeded
        var existing = _dataTypeService.GetAll().ToList();
        if (existing.Any(d => d.Name?.StartsWith("Test_") == true)) return;

        int created = 0;

        // 1. Create List View data types
        created += CreateListViewDataTypes(_config.DataTypes.ListView);

        // 2. Create MultiNodeTreePicker data types
        created += CreateMultiNodeTreePickerDataTypes(_config.DataTypes.MultiNodeTreePicker);

        // 3. Create Other data types
        created += CreateRichTextEditorDataTypes(_config.DataTypes.RichTextEditor);
        created += CreateMediaPickerDataTypes(_config.DataTypes.MediaPicker);
        created += CreateTextareaDataTypes(_config.DataTypes.Textarea);
        created += CreateDropdownDataTypes(_config.DataTypes.Dropdown);
        created += CreateNumericDataTypes(_config.DataTypes.Numeric);

        // Note: Block List and Block Grid will be created after Element Types
        // in DocumentTypeSeeder, as they require Element Types to be created first

        Console.WriteLine($"Seeded {created} data types (target: {_config.DataTypes.Total}). Block List and Block Grid will be created after Element Types.");
    }

    private int CreateListViewDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.ListView];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_ListView_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Nvarchar
            };

            _dataTypeService.Save(dataType);
            created++;

            if (created % 10 == 0)
                Console.WriteLine($"Created {created} ListView data types...");
        }

        return created;
    }

    private int CreateMultiNodeTreePickerDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.MultiNodeTreePicker];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_MNTP_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            // Configure to pick from content
            var config = new MultiNodePickerConfiguration
            {
                TreeSource = new MultiNodePickerConfigurationTreeSource
                {
                    ObjectType = "content"
                },
                MaxNumber = i % 5 + 1, // 1-5 max items
                MinNumber = 0,
                IgnoreUserStartNodes = false
            };
            dataType.Configuration = config;

            _dataTypeService.Save(dataType);
            created++;

            if (created % 10 == 0)
                Console.WriteLine($"Created {created} MultiNodeTreePicker data types...");
        }

        return created;
    }

    private int CreateRichTextEditorDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.TinyMce];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_RTE_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            _dataTypeService.Save(dataType);
            created++;
        }

        Console.WriteLine($"Created {created} RTE data types.");
        return created;
    }

    private int CreateMediaPickerDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.MediaPicker3];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_MediaPicker_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            _dataTypeService.Save(dataType);
            created++;
        }

        Console.WriteLine($"Created {created} MediaPicker data types.");
        return created;
    }

    private int CreateTextareaDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.TextArea];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_Textarea_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            _dataTypeService.Save(dataType);
            created++;
        }

        Console.WriteLine($"Created {created} Textarea data types.");
        return created;
    }

    private int CreateDropdownDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.DropDownListFlexible];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_Dropdown_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Nvarchar
            };

            // Add some items
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
                Multiple = i % 2 == 0 // Alternate between single/multiple
            };
            dataType.Configuration = config;

            _dataTypeService.Save(dataType);
            created++;
        }

        Console.WriteLine($"Created {created} Dropdown data types.");
        return created;
    }

    private int CreateNumericDataTypes(int count)
    {
        if (count <= 0) return 0;

        int created = 0;
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.Integer];
        if (editor == null) return 0;

        for (int i = 1; i <= count; i++)
        {
            var name = $"Test_Numeric_{i}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Integer
            };

            _dataTypeService.Save(dataType);
            created++;
        }

        Console.WriteLine($"Created {created} Numeric data types.");
        return created;
    }
}
