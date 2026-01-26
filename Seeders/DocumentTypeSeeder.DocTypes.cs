namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// Partial class containing Document Type and Template creation logic.
/// </summary>
public partial class DocumentTypeSeeder
{
    #region Variant Document Types

    private void CreateVariantDocumentTypes(ComplexityConfig config, List<IDataType> blockListDataTypes,
        List<IDataType> blockGridDataTypes, CancellationToken cancellationToken)
    {
        var prefix = GetPrefix("variantdoctype");
        int created = 0;

        // Simple
        for (int i = 1; i <= config.Simple; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alias = $"{prefix}Simple{i}";
                var name = $"Test Variant Simple {i}";
                var docType = CreateDocumentTypeWithTemplate(alias, name, "simple", true, null, null);
                Context.SimpleDocTypes.Add(docType);
                created++;
                LogProgress(created, config.Total, "variant document types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create simple variant doc type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        // Medium
        for (int i = 1; i <= config.Medium; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alias = $"{prefix}Medium{i}";
                var name = $"Test Variant Medium {i}";
                var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
                var docType = CreateDocumentTypeWithTemplate(alias, name, "medium", true, blockList, null);
                Context.MediumDocTypes.Add(docType);
                created++;
                LogProgress(created, config.Total, "variant document types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create medium variant doc type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        // Complex
        for (int i = 1; i <= config.Complex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alias = $"{prefix}Complex{i}";
                var name = $"Test Variant Complex {i}";
                var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
                var blockGrid = blockGridDataTypes.Count > 0 ? blockGridDataTypes[i % blockGridDataTypes.Count] : null;
                var docType = CreateDocumentTypeWithTemplate(alias, name, "complex", true, blockList, blockGrid);
                Context.ComplexDocTypes.Add(docType);
                created++;
                LogProgress(created, config.Total, "variant document types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create complex variant doc type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} variant document types (target: {Target})", created, config.Total);
    }

    #endregion

    #region Invariant Document Types

    private void CreateInvariantDocumentTypes(ComplexityConfig config, List<IDataType> blockListDataTypes,
        List<IDataType> blockGridDataTypes, CancellationToken cancellationToken)
    {
        var prefix = GetPrefix("invariantdoctype");
        int created = 0;

        // Simple
        for (int i = 1; i <= config.Simple; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alias = $"{prefix}Simple{i}";
                var name = $"Test Invariant Simple {i}";
                var docType = CreateDocumentTypeWithTemplate(alias, name, "simple", false, null, null);
                Context.SimpleDocTypes.Add(docType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create simple invariant doc type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        // Medium
        for (int i = 1; i <= config.Medium; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alias = $"{prefix}Medium{i}";
                var name = $"Test Invariant Medium {i}";
                var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
                var docType = CreateDocumentTypeWithTemplate(alias, name, "medium", false, blockList, null);
                Context.MediumDocTypes.Add(docType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create medium invariant doc type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        // Complex
        for (int i = 1; i <= config.Complex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alias = $"{prefix}Complex{i}";
                var name = $"Test Invariant Complex {i}";
                var blockList = blockListDataTypes.Count > 0 ? blockListDataTypes[i % blockListDataTypes.Count] : null;
                var blockGrid = blockGridDataTypes.Count > 0 ? blockGridDataTypes[i % blockGridDataTypes.Count] : null;
                var docType = CreateDocumentTypeWithTemplate(alias, name, "complex", false, blockList, blockGrid);
                Context.ComplexDocTypes.Add(docType);
                created++;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create complex invariant doc type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} invariant document types (target: {Target})", created, config.Total);
    }

    #endregion

    #region Document Type and Template Creation

    private IContentType CreateDocumentTypeWithTemplate(
        string alias,
        string name,
        string complexity,
        bool isVariant,
        IDataType? blockListDataType,
        IDataType? blockGridDataType)
    {
        // Create Template first
        var template = CreateTemplate(alias, name, complexity);

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
        return docType;
    }

    private ITemplate CreateTemplate(string alias, string name, string complexity)
    {
        var template = new Template(_shortStringHelper, name, alias)
        {
            Content = GenerateTemplateContent(alias, name, complexity)
        };

        _fileService.SaveTemplate(template);
        return template;
    }

    #endregion

    #region Template Content Generation

    private static string GenerateTemplateContent(string alias, string name, string complexity)
    {
        return complexity switch
        {
            "simple" => GenerateSimpleTemplateContent(alias, name),
            "medium" => GenerateMediumTemplateContent(alias, name),
            "complex" => GenerateComplexTemplateContent(alias, name),
            _ => GenerateSimpleTemplateContent(alias, name)
        };
    }

    private static string GenerateSimpleTemplateContent(string alias, string name) =>
        $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@{{
    Layout = null;
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>@Model.Name - {name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .property {{ margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; }}
        .property-label {{ font-weight: bold; }}
    </style>
</head>
<body>
    <h1>@Model.Name</h1>
    <p><em>Template: {alias}</em></p>
    @if (Model.HasValue(""title""))
    {{
        <div class=""property""><span class=""property-label"">Title:</span> @Model.Value(""title"")</div>
    }}
    @if (Model.HasValue(""description""))
    {{
        <div class=""property""><span class=""property-label"">Description:</span> @Model.Value(""description"")</div>
    }}
    <footer><p>Generated by PerformanceTestDataSeeder</p></footer>
</body>
</html>";

    private static string GenerateMediumTemplateContent(string alias, string name) =>
        $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Core.Models.Blocks
@{{
    Layout = null;
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>@Model.Name - {name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .property {{ margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; }}
        .property-label {{ font-weight: bold; }}
        .block-item {{ margin: 10px 0; padding: 15px; background: #f5f5f5; border-left: 3px solid #007bff; }}
    </style>
</head>
<body>
    <h1>@Model.Name</h1>
    <p><em>Template: {alias}</em></p>
    @if (Model.HasValue(""title"")) {{ <div class=""property""><span class=""property-label"">Title:</span> @Model.Value(""title"")</div> }}
    @if (Model.HasValue(""subtitle"")) {{ <div class=""property""><span class=""property-label"">Subtitle:</span> @Model.Value(""subtitle"")</div> }}
    @if (Model.HasValue(""summary"")) {{ <div class=""property""><span class=""property-label"">Summary:</span> @Model.Value(""summary"")</div> }}
    @if (Model.HasValue(""blocks""))
    {{
        var blocks = Model.Value<BlockListModel>(""blocks"");
        if (blocks != null && blocks.Any())
        {{
            <h2>Blocks</h2>
            foreach (var block in blocks)
            {{
                <div class=""block-item"">@block.Content.ContentType.Alias</div>
            }}
        }}
    }}
    <footer><p>Generated by PerformanceTestDataSeeder</p></footer>
</body>
</html>";

    private static string GenerateComplexTemplateContent(string alias, string name) =>
        $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Core.Models.Blocks
@using Umbraco.Cms.Core.Models.PublishedContent
@{{
    Layout = null;
}}
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>@Model.Name - {name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .property {{ margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; }}
        .property-label {{ font-weight: bold; }}
        .block-item {{ margin: 10px 0; padding: 15px; background: #f5f5f5; border-left: 3px solid #007bff; }}
        .grid-item {{ margin: 10px 0; padding: 15px; background: #e8f4e8; border-left: 3px solid #28a745; }}
        .media-image {{ max-width: 300px; }}
        section {{ margin-bottom: 30px; }}
        h2 {{ border-bottom: 2px solid #333; padding-bottom: 5px; }}
    </style>
</head>
<body>
    <h1>@Model.Name</h1>
    <p><em>Template: {alias}</em></p>
    <section>
        <h2>Content</h2>
        @if (Model.HasValue(""title"")) {{ <div class=""property""><span class=""property-label"">Title:</span> @Model.Value(""title"")</div> }}
        @if (Model.HasValue(""subtitle"")) {{ <div class=""property""><span class=""property-label"">Subtitle:</span> @Model.Value(""subtitle"")</div> }}
        @if (Model.HasValue(""bodyText"")) {{ <div class=""property""><span class=""property-label"">Body:</span> @Model.Value(""bodyText"")</div> }}
    </section>
    <section>
        <h2>Media</h2>
        @if (Model.HasValue(""mainImage""))
        {{
            var img = Model.Value<IPublishedContent>(""mainImage"");
            if (img != null) {{ <img src=""@img.Url()"" alt=""@img.Name"" class=""media-image"" /> }}
        }}
    </section>
    @if (Model.HasValue(""headerBlocks""))
    {{
        <section>
            <h2>Header Blocks</h2>
            @{{ var blocks = Model.Value<BlockListModel>(""headerBlocks""); }}
            @if (blocks != null) {{ foreach (var b in blocks) {{ <div class=""block-item"">@b.Content.ContentType.Alias</div> }} }}
        </section>
    }}
    @if (Model.HasValue(""mainGrid""))
    {{
        <section>
            <h2>Main Grid</h2>
            @{{ var grid = Model.Value<BlockGridModel>(""mainGrid""); }}
            @if (grid != null) {{ foreach (var g in grid) {{ <div class=""grid-item"">@g.Content.ContentType.Alias (Span: @g.ColumnSpan)</div> }} }}
        </section>
    }}
    <footer><p>Generated by PerformanceTestDataSeeder</p></footer>
</body>
</html>";

    #endregion

    #region Document Type Properties

    private void AddSimpleDocTypeProperties(IContentType docType)
    {
        var group = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };

        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1,
            Variations = docType.Variations
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextareaDataType())
        {
            Alias = "description",
            Name = "Description",
            SortOrder = 2,
            Variations = docType.Variations
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTrueFalseDataType())
        {
            Alias = "isPublished",
            Name = "Is Published",
            SortOrder = 3
        });

        docType.PropertyGroups.Add(group);
    }

    private void AddMediumDocTypeProperties(IContentType docType, IDataType? blockListDataType)
    {
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        { Alias = "title", Name = "Title", SortOrder = 1, Variations = docType.Variations });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        { Alias = "subtitle", Name = "Subtitle", SortOrder = 2, Variations = docType.Variations });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        { Alias = "summary", Name = "Summary", SortOrder = 3, Variations = docType.Variations });

        var relationsGroup = new PropertyGroup(true) { Alias = "relations", Name = "Relations", SortOrder = 2 };
        if (Context.ContentPickerDataType != null)
        {
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "relatedContent", Name = "Related Content", SortOrder = 1 });
        }
        if (blockListDataType != null)
        {
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
            { Alias = "blocks", Name = "Blocks", SortOrder = 2 });
        }

        docType.PropertyGroups.Add(contentGroup);
        docType.PropertyGroups.Add(relationsGroup);
    }

    private void AddComplexDocTypeProperties(IContentType docType, IDataType? blockListDataType, IDataType? blockGridDataType)
    {
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        { Alias = "title", Name = "Title", SortOrder = 1, Variations = docType.Variations });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        { Alias = "subtitle", Name = "Subtitle", SortOrder = 2, Variations = docType.Variations });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextareaDataType())
        { Alias = "bodyText", Name = "Body Text", SortOrder = 3, Variations = docType.Variations });

        var mediaGroup = new PropertyGroup(true) { Alias = "media", Name = "Media", SortOrder = 2 };
        if (Context.MediaPickerDataType != null)
        {
            mediaGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.MediaPickerDataType)
            { Alias = "mainImage", Name = "Main Image", SortOrder = 1 });
            mediaGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.MediaPickerDataType)
            { Alias = "thumbnailImage", Name = "Thumbnail Image", SortOrder = 2 });
        }

        var relationsGroup = new PropertyGroup(true) { Alias = "relations", Name = "Relations", SortOrder = 3 };
        if (Context.ContentPickerDataType != null)
        {
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "primaryContent", Name = "Primary Content", SortOrder = 1 });
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "secondaryContent", Name = "Secondary Content", SortOrder = 2 });
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "tertiaryContent", Name = "Tertiary Content", SortOrder = 3 });
        }

        var blocksGroup = new PropertyGroup(true) { Alias = "blocks", Name = "Blocks", SortOrder = 4 };
        if (blockListDataType != null)
        {
            blocksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
            { Alias = "headerBlocks", Name = "Header Blocks", SortOrder = 1 });
            blocksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
            { Alias = "footerBlocks", Name = "Footer Blocks", SortOrder = 2 });
        }
        if (blockGridDataType != null)
        {
            blocksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockGridDataType)
            { Alias = "mainGrid", Name = "Main Grid", SortOrder = 3 });
        }

        docType.PropertyGroups.Add(contentGroup);
        docType.PropertyGroups.Add(mediaGroup);
        docType.PropertyGroups.Add(relationsGroup);
        docType.PropertyGroups.Add(blocksGroup);
    }

    #endregion
}
