using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

public class DocumentTypeSeeder : IHostedService
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IFileService _fileService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    // Cache for created data types
    private readonly List<IContentType> _elementTypes = new();
    private IDataType? _textstringDataType;
    private IDataType? _textareaDataType;
    private IDataType? _trueFalseDataType;
    private IDataType? _labelDataType;
    private IDataType? _contentPickerDataType;
    private IDataType? _mediaPickerDataType;
    private IDataType? _uploadDataType;

    public DocumentTypeSeeder(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IFileService fileService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer,
        IShortStringHelper shortStringHelper,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _fileService = fileService;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
        _shortStringHelper = shortStringHelper;
        _runtimeState = runtimeState;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed when Umbraco is fully installed and running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            Console.WriteLine("DocumentTypeSeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

        SeedDocumentTypes();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedDocumentTypes()
    {
        // Check if already seeded
        var existingTypes = _contentTypeService.GetAll().ToList();
        if (existingTypes.Any(t => t.Alias.StartsWith("testElement") || t.Alias.StartsWith("testVariant"))) return;

        // Load built-in data types
        LoadBuiltInDataTypes();

        var docTypeConfig = _config.DocumentTypes;

        // Phase 1: Create Element Types - needed for Block List/Grid
        Console.WriteLine("Creating Element Types...");
        CreateElementTypes(docTypeConfig.ElementTypes);

        // Phase 2: Create Block List data types - 1 block each
        Console.WriteLine("Creating Block List data types...");
        var blockListDataTypes = CreateBlockListDataTypes(docTypeConfig.BlockList);

        // Phase 3: Create Block Grid data types - 30 blocks each
        Console.WriteLine("Creating Block Grid data types...");
        var blockGridDataTypes = CreateBlockGridDataTypes(docTypeConfig.BlockGrid);

        // Phase 4: Create Variant Document Types with Templates
        Console.WriteLine("Creating Variant Document Types with Templates...");
        CreateVariantDocumentTypes(docTypeConfig.VariantDocTypes, blockListDataTypes, blockGridDataTypes);

        // Phase 5: Create Invariant Document Types with Templates
        Console.WriteLine("Creating Invariant Document Types with Templates...");
        CreateInvariantDocumentTypes(docTypeConfig.InvariantDocTypes, blockListDataTypes, blockGridDataTypes);

        Console.WriteLine($"Seeded {_elementTypes.Count} Element Types, {blockListDataTypes.Count} Block List, {blockGridDataTypes.Count} Block Grid, and {docTypeConfig.TotalDocTypes} Document Types with Templates.");
    }

    private void LoadBuiltInDataTypes()
    {
        _textstringDataType = _dataTypeService.GetDataType(Constants.DataTypes.Textbox);
        _textareaDataType = _dataTypeService.GetDataType(Constants.DataTypes.Textarea);
        _trueFalseDataType = _dataTypeService.GetDataType(Constants.DataTypes.Boolean);
        _labelDataType = _dataTypeService.GetDataType(Constants.DataTypes.LabelString);
        _uploadDataType = _dataTypeService.GetDataType(Constants.DataTypes.Upload);

        // Get Content Picker and Media Picker by name (as they don't have constant IDs)
        var allDataTypes = _dataTypeService.GetAll().ToList();
        _contentPickerDataType = allDataTypes.FirstOrDefault(d => d.EditorAlias == Constants.PropertyEditors.Aliases.ContentPicker);
        _mediaPickerDataType = allDataTypes.FirstOrDefault(d => d.EditorAlias == Constants.PropertyEditors.Aliases.MediaPicker3);
    }

    #region Element Types

    private void CreateElementTypes(ComplexityConfig config)
    {
        // Simple (3 properties, 1 tab)
        for (int i = 1; i <= config.Simple; i++)
        {
            var elementType = CreateElementType($"testElementSimple{i}", $"Test Element Simple {i}", "simple");
            _elementTypes.Add(elementType);

            if (i % 20 == 0) Console.WriteLine($"Created {i} simple element types...");
        }

        // Medium (5 properties, 2 tabs)
        for (int i = 1; i <= config.Medium; i++)
        {
            var elementType = CreateElementType($"testElementMedium{i}", $"Test Element Medium {i}", "medium");
            _elementTypes.Add(elementType);

            if (i % 10 == 0) Console.WriteLine($"Created {i} medium element types...");
        }

        // Complex (10 properties, 5 tabs)
        for (int i = 1; i <= config.Complex; i++)
        {
            var elementType = CreateElementType($"testElementComplex{i}", $"Test Element Complex {i}", "complex");
            _elementTypes.Add(elementType);

            if (i % 10 == 0) Console.WriteLine($"Created {i} complex element types...");
        }

        Console.WriteLine($"Created {_elementTypes.Count} element types total (target: {config.Total}).");
    }

    private IContentType CreateElementType(string alias, string name, string complexity)
    {
        var elementType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            IsElement = true,
            Icon = "icon-brick"
        };

        switch (complexity)
        {
            case "simple":
                AddSimpleProperties(elementType);
                break;
            case "medium":
                AddMediumProperties(elementType);
                break;
            case "complex":
                AddComplexProperties(elementType);
                break;
        }

        _contentTypeService.Save(elementType);
        return elementType;
    }

    private void AddSimpleProperties(IContentType contentType)
    {
        // 3 properties, 1 tab: textstring, textarea, true/false
        var group = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };

        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1
        });
        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textareaDataType!)
        {
            Alias = "description",
            Name = "Description",
            SortOrder = 2
        });
        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, _trueFalseDataType!)
        {
            Alias = "isActive",
            Name = "Is Active",
            SortOrder = 3
        });

        contentType.PropertyGroups.Add(group);
    }

    private void AddMediumProperties(IContentType contentType)
    {
        // 5 properties, 2 tabs
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "subtitle",
            Name = "Subtitle",
            SortOrder = 2
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textareaDataType!)
        {
            Alias = "description",
            Name = "Description",
            SortOrder = 3
        });

        var settingsGroup = new PropertyGroup(true) { Alias = "settings", Name = "Settings", SortOrder = 2 };
        settingsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _trueFalseDataType!)
        {
            Alias = "isVisible",
            Name = "Is Visible",
            SortOrder = 1
        });
        settingsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "linkedContent",
            Name = "Linked Content",
            SortOrder = 2
        });

        contentType.PropertyGroups.Add(contentGroup);
        contentType.PropertyGroups.Add(settingsGroup);
    }

    private void AddComplexProperties(IContentType contentType)
    {
        // 10 properties, 5 tabs
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textareaDataType!)
        {
            Alias = "summary",
            Name = "Summary",
            SortOrder = 2
        });

        var mediaGroup = new PropertyGroup(true) { Alias = "media", Name = "Media", SortOrder = 2 };
        mediaGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _mediaPickerDataType!)
        {
            Alias = "mainImage",
            Name = "Main Image",
            SortOrder = 1
        });
        mediaGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _mediaPickerDataType!)
        {
            Alias = "thumbnailImage",
            Name = "Thumbnail Image",
            SortOrder = 2
        });

        var linksGroup = new PropertyGroup(true) { Alias = "links", Name = "Links", SortOrder = 3 };
        linksGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "primaryLink",
            Name = "Primary Link",
            SortOrder = 1
        });
        linksGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "secondaryLink",
            Name = "Secondary Link",
            SortOrder = 2
        });

        var settingsGroup = new PropertyGroup(true) { Alias = "settings", Name = "Settings", SortOrder = 4 };
        settingsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _trueFalseDataType!)
        {
            Alias = "isEnabled",
            Name = "Is Enabled",
            SortOrder = 1
        });
        settingsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "cssClass",
            Name = "CSS Class",
            SortOrder = 2
        });

        var seoGroup = new PropertyGroup(true) { Alias = "seo", Name = "SEO", SortOrder = 5 };
        seoGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "metaTitle",
            Name = "Meta Title",
            SortOrder = 1
        });
        seoGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textareaDataType!)
        {
            Alias = "metaDescription",
            Name = "Meta Description",
            SortOrder = 2
        });

        contentType.PropertyGroups.Add(contentGroup);
        contentType.PropertyGroups.Add(mediaGroup);
        contentType.PropertyGroups.Add(linksGroup);
        contentType.PropertyGroups.Add(settingsGroup);
        contentType.PropertyGroups.Add(seoGroup);
    }

    #endregion

    #region Block List Data Types (40)

    private List<IDataType> CreateBlockListDataTypes(int count)
    {
        var blockListDataTypes = new List<IDataType>();
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockList];
        if (editor == null) return blockListDataTypes;

        // Categorize element types by complexity
        var simpleElements = _elementTypes.Where(e => e.Alias.Contains("Simple")).ToList();
        var mediumElements = _elementTypes.Where(e => e.Alias.Contains("Medium")).ToList();
        var complexElements = _elementTypes.Where(e => e.Alias.Contains("Complex")).ToList();

        for (int i = 0; i < count; i++)
        {
            var name = $"Test_BlockList_{i + 1}";

            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            // Configure with 3 blocks: 1 Simple + 1 Medium + 1 Complex
            var blocks = new List<BlockListConfiguration.BlockConfiguration>();

            // Add a simple element type
            if (simpleElements.Count > 0)
            {
                var simpleElement = simpleElements[i % simpleElements.Count];
                blocks.Add(new BlockListConfiguration.BlockConfiguration
                {
                    ContentElementTypeKey = simpleElement.Key,
                    Label = simpleElement.Name
                });
            }

            // Add a medium element type
            if (mediumElements.Count > 0)
            {
                var mediumElement = mediumElements[i % mediumElements.Count];
                blocks.Add(new BlockListConfiguration.BlockConfiguration
                {
                    ContentElementTypeKey = mediumElement.Key,
                    Label = mediumElement.Name
                });
            }

            // Add a complex element type
            if (complexElements.Count > 0)
            {
                var complexElement = complexElements[i % complexElements.Count];
                blocks.Add(new BlockListConfiguration.BlockConfiguration
                {
                    ContentElementTypeKey = complexElement.Key,
                    Label = complexElement.Name
                });
            }

            var config = new BlockListConfiguration
            {
                Blocks = blocks.ToArray()
            };
            dataType.Configuration = config;

            _dataTypeService.Save(dataType);
            blockListDataTypes.Add(dataType);

            if ((i + 1) % 10 == 0)
                Console.WriteLine($"Created {i + 1} Block List data types...");
        }

        return blockListDataTypes;
    }

    #endregion

    #region Block Grid Data Types (60)

    private List<IDataType> CreateBlockGridDataTypes(int count)
    {
        var blockGridDataTypes = new List<IDataType>();
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockGrid];
        if (editor == null) return blockGridDataTypes;

        for (int i = 0; i < count; i++)
        {
            var name = $"Test_BlockGrid_{i + 1}";

            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            // Configure with 30 blocks (reuse element types cyclically)
            var blocks = new List<BlockGridConfiguration.BlockGridBlockConfiguration>();
            for (int j = 0; j < 30 && j < _elementTypes.Count; j++)
            {
                var elementType = _elementTypes[j % _elementTypes.Count];
                blocks.Add(new BlockGridConfiguration.BlockGridBlockConfiguration
                {
                    ContentElementTypeKey = elementType.Key,
                    Label = elementType.Name,
                    AllowAtRoot = true,
                    AllowInAreas = true
                });
            }

            var config = new BlockGridConfiguration
            {
                Blocks = blocks.ToArray(),
                GridColumns = 12
            };
            dataType.Configuration = config;

            _dataTypeService.Save(dataType);
            blockGridDataTypes.Add(dataType);

            if ((i + 1) % 10 == 0)
                Console.WriteLine($"Created {i + 1} Block Grid data types...");
        }

        return blockGridDataTypes;
    }

    #endregion

    #region Variant Document Types

    private void CreateVariantDocumentTypes(ComplexityConfig config, List<IDataType> blockListDataTypes, List<IDataType> blockGridDataTypes)
    {
        int created = 0;

        // Simple
        for (int i = 1; i <= config.Simple; i++)
        {
            var alias = $"testVariantSimple{i}";
            var name = $"Test Variant Simple {i}";
            CreateDocumentTypeWithTemplate(alias, name, "simple", true, null, null);
            created++;
            if (created % 20 == 0) Console.WriteLine($"Created {created} variant document types...");
        }

        // Medium
        for (int i = 1; i <= config.Medium; i++)
        {
            var alias = $"testVariantMedium{i}";
            var name = $"Test Variant Medium {i}";
            var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
            CreateDocumentTypeWithTemplate(alias, name, "medium", true, blockList, null);
            created++;
            if (created % 20 == 0) Console.WriteLine($"Created {created} variant document types...");
        }

        // Complex
        for (int i = 1; i <= config.Complex; i++)
        {
            var alias = $"testVariantComplex{i}";
            var name = $"Test Variant Complex {i}";
            var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
            var blockGrid = blockGridDataTypes.Count > 0 ? blockGridDataTypes[i % blockGridDataTypes.Count] : null;
            CreateDocumentTypeWithTemplate(alias, name, "complex", true, blockList, blockGrid);
            created++;
            if (created % 20 == 0) Console.WriteLine($"Created {created} variant document types...");
        }

        Console.WriteLine($"Created {created} variant document types (target: {config.Total}).");
    }

    #endregion

    #region Invariant Document Types

    private void CreateInvariantDocumentTypes(ComplexityConfig config, List<IDataType> blockListDataTypes, List<IDataType> blockGridDataTypes)
    {
        int created = 0;

        // Simple
        for (int i = 1; i <= config.Simple; i++)
        {
            var alias = $"testInvariantSimple{i}";
            var name = $"Test Invariant Simple {i}";
            CreateDocumentTypeWithTemplate(alias, name, "simple", false, null, null);
            created++;
        }

        Console.WriteLine($"Created {created} simple invariant document types.");

        // Medium
        for (int i = 1; i <= config.Medium; i++)
        {
            var alias = $"testInvariantMedium{i}";
            var name = $"Test Invariant Medium {i}";
            var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
            CreateDocumentTypeWithTemplate(alias, name, "medium", false, blockList, null);
            created++;
        }

        Console.WriteLine($"Created {created} invariant document types (including medium).");

        // Complex
        for (int i = 1; i <= config.Complex; i++)
        {
            var alias = $"testInvariantComplex{i}";
            var name = $"Test Invariant Complex {i}";
            var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
            var blockGrid = blockGridDataTypes.Count > 0 ? blockGridDataTypes[i % blockGridDataTypes.Count] : null;
            CreateDocumentTypeWithTemplate(alias, name, "complex", false, blockList, blockGrid);
            created++;
        }

        Console.WriteLine($"Created {created} invariant document types total.");
    }

    #endregion

    #region Create Document Type with Template

    private void CreateDocumentTypeWithTemplate(
        string alias,
        string name,
        string complexity,
        bool isVariant,
        IDataType? blockListDataType,
        IDataType? blockGridDataType)
    {
        // Create Template first
        var template = CreateTemplate(alias, name);

        // Create Document Type
        var docType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            Icon = isVariant ? "icon-document" : "icon-document-dashed-line",
            AllowedAsRoot = true,
            Variations = isVariant ? ContentVariation.Culture : ContentVariation.Nothing
        };

        // Add properties based on complexity
        switch (complexity)
        {
            case "simple":
                AddSimpleDocTypeProperties(docType);
                break;
            case "medium":
                AddMediumDocTypeProperties(docType, blockListDataType);
                break;
            case "complex":
                AddComplexDocTypeProperties(docType, blockListDataType, blockGridDataType);
                break;
        }

        // Set allowed templates
        docType.AllowedTemplates = new[] { template };
        docType.SetDefaultTemplate(template);

        _contentTypeService.Save(docType);
    }

    private ITemplate CreateTemplate(string alias, string name)
    {
        var template = new Template(_shortStringHelper, name, alias)
        {
            Content = GenerateTemplateContent(alias, name)
        };

        _fileService.SaveTemplate(template);
        return template;
    }

    private string GenerateTemplateContent(string alias, string name)
    {
        // Determine complexity based on alias
        string complexity = "simple";
        if (alias.Contains("Medium")) complexity = "medium";
        else if (alias.Contains("Complex")) complexity = "complex";

        return complexity switch
        {
            "simple" => GenerateSimpleTemplateContent(alias, name),
            "medium" => GenerateMediumTemplateContent(alias, name),
            "complex" => GenerateComplexTemplateContent(alias, name),
            _ => GenerateSimpleTemplateContent(alias, name)
        };
    }

    private string GenerateSimpleTemplateContent(string alias, string name)
    {
        return $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{{
    Layout = null;
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>@Model.Name - {name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .property {{ margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; }}
        .property-label {{ font-weight: bold; color: #333; }}
        .property-value {{ margin-top: 5px; }}
    </style>
</head>
<body>
    <header>
        <h1>@Model.Name</h1>
        <p><em>Template: {alias}</em></p>
    </header>
    <main>
        <article>
            @if (Model.HasValue(""title""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Title:</div>
                    <div class=""property-value"">@Model.Value(""title"")</div>
                </div>
            }}
            @if (Model.HasValue(""description""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Description:</div>
                    <div class=""property-value"">@Model.Value(""description"")</div>
                </div>
            }}
            @if (Model.HasProperty(""isPublished""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Is Published:</div>
                    <div class=""property-value"">@Model.Value(""isPublished"")</div>
                </div>
            }}
        </article>
    </main>
    <footer>
        <p>Generated by TestData Seeder</p>
    </footer>
</body>
</html>";
    }

    private string GenerateMediumTemplateContent(string alias, string name)
    {
        return $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Core.Models.Blocks
@{{
    Layout = null;
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>@Model.Name - {name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .property {{ margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; }}
        .property-label {{ font-weight: bold; color: #333; }}
        .property-value {{ margin-top: 5px; }}
        .block-item {{ margin: 10px 0; padding: 15px; background: #f5f5f5; border-left: 3px solid #007bff; }}
        .block-title {{ font-weight: bold; color: #007bff; }}
    </style>
</head>
<body>
    <header>
        <h1>@Model.Name</h1>
        <p><em>Template: {alias}</em></p>
    </header>
    <main>
        <article>
            <!-- Text Properties -->
            @if (Model.HasValue(""title""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Title:</div>
                    <div class=""property-value"">@Model.Value(""title"")</div>
                </div>
            }}
            @if (Model.HasValue(""subtitle""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Subtitle:</div>
                    <div class=""property-value"">@Model.Value(""subtitle"")</div>
                </div>
            }}
            @if (Model.HasValue(""summary""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Summary:</div>
                    <div class=""property-value"">@Model.Value(""summary"")</div>
                </div>
            }}

            <!-- Related Content -->
            @if (Model.HasValue(""relatedContent""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Related Content:</div>
                    <div class=""property-value"">
                        @{{
                            var relatedContent = Model.Value<Umbraco.Cms.Core.Models.PublishedContent.IPublishedContent>(""relatedContent"");
                            if (relatedContent != null)
                            {{
                                <a href=""@relatedContent.Url()"">@relatedContent.Name</a>
                            }}
                        }}
                    </div>
                </div>
            }}

            <!-- Block List -->
            @if (Model.HasValue(""blocks""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Blocks:</div>
                    <div class=""property-value"">
                        @{{
                            var blocks = Model.Value<BlockListModel>(""blocks"");
                            if (blocks != null && blocks.Any())
                            {{
                                foreach (var block in blocks)
                                {{
                                    <div class=""block-item"">
                                        <div class=""block-title"">@block.Content.ContentType.Alias</div>
                                        @foreach (var prop in block.Content.Properties)
                                        {{
                                            <div><strong>@prop.Alias:</strong> @block.Content.Value(prop.Alias)</div>
                                        }}
                                    </div>
                                }}
                            }}
                        }}
                    </div>
                </div>
            }}
        </article>
    </main>
    <footer>
        <p>Generated by TestData Seeder</p>
    </footer>
</body>
</html>";
    }

    private string GenerateComplexTemplateContent(string alias, string name)
    {
        return $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Core.Models.Blocks
@using Umbraco.Cms.Core.Models.PublishedContent
@{{
    Layout = null;
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>@Model.Name - {name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .property {{ margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; }}
        .property-label {{ font-weight: bold; color: #333; }}
        .property-value {{ margin-top: 5px; }}
        .block-item {{ margin: 10px 0; padding: 15px; background: #f5f5f5; border-left: 3px solid #007bff; }}
        .block-title {{ font-weight: bold; color: #007bff; }}
        .grid-item {{ margin: 10px 0; padding: 15px; background: #e8f4e8; border-left: 3px solid #28a745; }}
        .grid-title {{ font-weight: bold; color: #28a745; }}
        .media-image {{ max-width: 300px; height: auto; }}
        section {{ margin-bottom: 30px; }}
        h2 {{ border-bottom: 2px solid #333; padding-bottom: 5px; }}
    </style>
</head>
<body>
    <header>
        <h1>@Model.Name</h1>
        <p><em>Template: {alias}</em></p>
    </header>
    <main>
        <!-- Content Section -->
        <section>
            <h2>Content</h2>
            @if (Model.HasValue(""title""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Title:</div>
                    <div class=""property-value"">@Model.Value(""title"")</div>
                </div>
            }}
            @if (Model.HasValue(""subtitle""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Subtitle:</div>
                    <div class=""property-value"">@Model.Value(""subtitle"")</div>
                </div>
            }}
            @if (Model.HasValue(""bodyText""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Body Text:</div>
                    <div class=""property-value"">@Model.Value(""bodyText"")</div>
                </div>
            }}
        </section>

        <!-- Media Section -->
        <section>
            <h2>Media</h2>
            @if (Model.HasValue(""mainImage""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Main Image:</div>
                    <div class=""property-value"">
                        @{{
                            var mainImage = Model.Value<IPublishedContent>(""mainImage"");
                            if (mainImage != null)
                            {{
                                <img src=""@mainImage.Url()"" alt=""@mainImage.Name"" class=""media-image"" />
                                <p>@mainImage.Name</p>
                            }}
                        }}
                    </div>
                </div>
            }}
            @if (Model.HasValue(""thumbnailImage""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Thumbnail Image:</div>
                    <div class=""property-value"">
                        @{{
                            var thumbnailImage = Model.Value<IPublishedContent>(""thumbnailImage"");
                            if (thumbnailImage != null)
                            {{
                                <img src=""@thumbnailImage.Url()"" alt=""@thumbnailImage.Name"" class=""media-image"" />
                                <p>@thumbnailImage.Name</p>
                            }}
                        }}
                    </div>
                </div>
            }}
        </section>

        <!-- Relations Section -->
        <section>
            <h2>Related Content</h2>
            @if (Model.HasValue(""primaryContent""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Primary Content:</div>
                    <div class=""property-value"">
                        @{{
                            var primaryContent = Model.Value<IPublishedContent>(""primaryContent"");
                            if (primaryContent != null)
                            {{
                                <a href=""@primaryContent.Url()"">@primaryContent.Name</a>
                            }}
                        }}
                    </div>
                </div>
            }}
            @if (Model.HasValue(""secondaryContent""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Secondary Content:</div>
                    <div class=""property-value"">
                        @{{
                            var secondaryContent = Model.Value<IPublishedContent>(""secondaryContent"");
                            if (secondaryContent != null)
                            {{
                                <a href=""@secondaryContent.Url()"">@secondaryContent.Name</a>
                            }}
                        }}
                    </div>
                </div>
            }}
            @if (Model.HasValue(""tertiaryContent""))
            {{
                <div class=""property"">
                    <div class=""property-label"">Tertiary Content:</div>
                    <div class=""property-value"">
                        @{{
                            var tertiaryContent = Model.Value<IPublishedContent>(""tertiaryContent"");
                            if (tertiaryContent != null)
                            {{
                                <a href=""@tertiaryContent.Url()"">@tertiaryContent.Name</a>
                            }}
                        }}
                    </div>
                </div>
            }}
        </section>

        <!-- Block List Section - Header Blocks -->
        <section>
            <h2>Header Blocks</h2>
            @if (Model.HasValue(""headerBlocks""))
            {{
                <div class=""property"">
                    @{{
                        var headerBlocks = Model.Value<BlockListModel>(""headerBlocks"");
                        if (headerBlocks != null && headerBlocks.Any())
                        {{
                            foreach (var block in headerBlocks)
                            {{
                                <div class=""block-item"">
                                    <div class=""block-title"">@block.Content.ContentType.Alias</div>
                                    @foreach (var prop in block.Content.Properties)
                                    {{
                                        <div><strong>@prop.Alias:</strong> @block.Content.Value(prop.Alias)</div>
                                    }}
                                </div>
                            }}
                        }}
                        else
                        {{
                            <p>No header blocks</p>
                        }}
                    }}
                </div>
            }}
        </section>

        <!-- Block List Section - Footer Blocks -->
        <section>
            <h2>Footer Blocks</h2>
            @if (Model.HasValue(""footerBlocks""))
            {{
                <div class=""property"">
                    @{{
                        var footerBlocks = Model.Value<BlockListModel>(""footerBlocks"");
                        if (footerBlocks != null && footerBlocks.Any())
                        {{
                            foreach (var block in footerBlocks)
                            {{
                                <div class=""block-item"">
                                    <div class=""block-title"">@block.Content.ContentType.Alias</div>
                                    @foreach (var prop in block.Content.Properties)
                                    {{
                                        <div><strong>@prop.Alias:</strong> @block.Content.Value(prop.Alias)</div>
                                    }}
                                </div>
                            }}
                        }}
                        else
                        {{
                            <p>No footer blocks</p>
                        }}
                    }}
                </div>
            }}
        </section>

        <!-- Block Grid Section -->
        <section>
            <h2>Main Grid</h2>
            @if (Model.HasValue(""mainGrid""))
            {{
                <div class=""property"">
                    @{{
                        var mainGrid = Model.Value<BlockGridModel>(""mainGrid"");
                        if (mainGrid != null && mainGrid.Any())
                        {{
                            foreach (var gridItem in mainGrid)
                            {{
                                <div class=""grid-item"">
                                    <div class=""grid-title"">@gridItem.Content.ContentType.Alias (Column Span: @gridItem.ColumnSpan, Row Span: @gridItem.RowSpan)</div>
                                    @foreach (var prop in gridItem.Content.Properties)
                                    {{
                                        <div><strong>@prop.Alias:</strong> @gridItem.Content.Value(prop.Alias)</div>
                                    }}
                                    @if (gridItem.Areas.Any())
                                    {{
                                        <div style=""margin-left: 20px; margin-top: 10px;"">
                                            <strong>Areas:</strong>
                                            @foreach (var area in gridItem.Areas)
                                            {{
                                                <div style=""margin-left: 10px;"">
                                                    <em>Area: @area.Alias</em>
                                                    @foreach (var areaItem in area)
                                                    {{
                                                        <div class=""grid-item"">
                                                            <div class=""grid-title"">@areaItem.Content.ContentType.Alias</div>
                                                            @foreach (var prop in areaItem.Content.Properties)
                                                            {{
                                                                <div><strong>@prop.Alias:</strong> @areaItem.Content.Value(prop.Alias)</div>
                                                            }}
                                                        </div>
                                                    }}
                                                </div>
                                            }}
                                        </div>
                                    }}
                                </div>
                            }}
                        }}
                        else
                        {{
                            <p>No grid items</p>
                        }}
                    }}
                </div>
            }}
        </section>
    </main>
    <footer>
        <p>Generated by TestData Seeder</p>
    </footer>
</body>
</html>";
    }

    private void AddSimpleDocTypeProperties(IContentType docType)
    {
        // 3 properties: textstring, textarea, true/false
        var group = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };

        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1,
            Variations = docType.Variations
        });
        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textareaDataType!)
        {
            Alias = "description",
            Name = "Description",
            SortOrder = 2,
            Variations = docType.Variations
        });
        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, _trueFalseDataType!)
        {
            Alias = "isPublished",
            Name = "Is Published",
            SortOrder = 3
        });

        docType.PropertyGroups.Add(group);
    }

    private void AddMediumDocTypeProperties(IContentType docType, IDataType? blockListDataType)
    {
        // 5 properties: 3 textstring, 1 content picker, 1 block
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1,
            Variations = docType.Variations
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "subtitle",
            Name = "Subtitle",
            SortOrder = 2,
            Variations = docType.Variations
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "summary",
            Name = "Summary",
            SortOrder = 3,
            Variations = docType.Variations
        });

        var relationsGroup = new PropertyGroup(true) { Alias = "relations", Name = "Relations", SortOrder = 2 };
        relationsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "relatedContent",
            Name = "Related Content",
            SortOrder = 1
        });

        if (blockListDataType != null)
        {
            relationsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, blockListDataType)
            {
                Alias = "blocks",
                Name = "Blocks",
                SortOrder = 2
            });
        }

        docType.PropertyGroups.Add(contentGroup);
        docType.PropertyGroups.Add(relationsGroup);
    }

    private void AddComplexDocTypeProperties(IContentType docType, IDataType? blockListDataType, IDataType? blockGridDataType)
    {
        // 10 properties: 3 textstring, 1 upload, 3 content pickers, 3 blocks
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1,
            Variations = docType.Variations
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textstringDataType!)
        {
            Alias = "subtitle",
            Name = "Subtitle",
            SortOrder = 2,
            Variations = docType.Variations
        });
        contentGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _textareaDataType!)
        {
            Alias = "bodyText",
            Name = "Body Text",
            SortOrder = 3,
            Variations = docType.Variations
        });

        var mediaGroup = new PropertyGroup(true) { Alias = "media", Name = "Media", SortOrder = 2 };
        mediaGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _mediaPickerDataType!)
        {
            Alias = "mainImage",
            Name = "Main Image",
            SortOrder = 1
        });
        mediaGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _mediaPickerDataType!)
        {
            Alias = "thumbnailImage",
            Name = "Thumbnail Image",
            SortOrder = 2
        });

        var relationsGroup = new PropertyGroup(true) { Alias = "relations", Name = "Relations", SortOrder = 3 };
        relationsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "primaryContent",
            Name = "Primary Content",
            SortOrder = 1
        });
        relationsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "secondaryContent",
            Name = "Secondary Content",
            SortOrder = 2
        });
        relationsGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, _contentPickerDataType!)
        {
            Alias = "tertiaryContent",
            Name = "Tertiary Content",
            SortOrder = 3
        });

        var blocksGroup = new PropertyGroup(true) { Alias = "blocks", Name = "Blocks", SortOrder = 4 };
        if (blockListDataType != null)
        {
            blocksGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, blockListDataType)
            {
                Alias = "headerBlocks",
                Name = "Header Blocks",
                SortOrder = 1
            });
            blocksGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, blockListDataType)
            {
                Alias = "footerBlocks",
                Name = "Footer Blocks",
                SortOrder = 2
            });
        }
        if (blockGridDataType != null)
        {
            blocksGroup.PropertyTypes.Add(new PropertyType(_shortStringHelper, blockGridDataType)
            {
                Alias = "mainGrid",
                Name = "Main Grid",
                SortOrder = 3
            });
        }

        docType.PropertyGroups.Add(contentGroup);
        docType.PropertyGroups.Add(mediaGroup);
        docType.PropertyGroups.Add(relationsGroup);
        docType.PropertyGroups.Add(blocksGroup);
    }

    #endregion
}
