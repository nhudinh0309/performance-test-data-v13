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
        var prefix = GetPrefix(PrefixType.VariantDocType);
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
                Context.AddSimpleDocType(docType);
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
                Context.AddMediumDocType(docType);
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
                Context.AddComplexDocType(docType);
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
        var prefix = GetPrefix(PrefixType.InvariantDocType);
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
                Context.AddSimpleDocType(docType);
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
                Context.AddMediumDocType(docType);
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
                Context.AddComplexDocType(docType);
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
        .status-published {{ color: green; }}
        .status-unpublished {{ color: orange; }}
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
    @if (Model.HasProperty(""isPublished""))
    {{
        var isPublished = Model.Value<bool>(""isPublished"");
        <div class=""property"">
            <span class=""property-label"">Status:</span>
            <span class=""@(isPublished ? ""status-published"" : ""status-unpublished"")"">@(isPublished ? ""Published"" : ""Draft"")</span>
        </div>
    }}
    <footer><p>Generated by PerformanceTestDataSeeder</p></footer>
</body>
</html>";

    private static string GenerateMediumTemplateContent(string alias, string name) =>
        $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Core.Models.Blocks
@using Umbraco.Cms.Core.Models
@using Umbraco.Cms.Core.Models.PublishedContent
@{{
    Layout = null;

    void RenderBlockContent(IPublishedElement content, int depth = 0)
    {{
        <div class=""block-content"" style=""margin-left: @(depth * 20)px"">
            <div class=""block-header"">@content.ContentType.Alias</div>
            @if (content.HasValue(""title"")) {{ <div class=""block-prop""><strong>Title:</strong> @content.Value(""title"")</div> }}
            @if (content.HasValue(""subtitle"")) {{ <div class=""block-prop""><strong>Subtitle:</strong> @content.Value(""subtitle"")</div> }}
            @if (content.HasValue(""description"")) {{ <div class=""block-prop""><strong>Description:</strong> @content.Value(""description"")</div> }}
            @if (content.HasValue(""summary"")) {{ <div class=""block-prop""><strong>Summary:</strong> @content.Value(""summary"")</div> }}
            @if (content.HasValue(""containerTitle"")) {{ <div class=""block-prop""><strong>Container:</strong> @content.Value(""containerTitle"")</div> }}
            @if (content.HasValue(""isActive"")) {{ <div class=""block-prop""><strong>Active:</strong> @content.Value(""isActive"")</div> }}
            @if (content.HasValue(""isVisible"")) {{ <div class=""block-prop""><strong>Visible:</strong> @content.Value(""isVisible"")</div> }}
            @if (content.HasValue(""mainImage""))
            {{
                var img = content.Value<IEnumerable<MediaWithCrops>>(""mainImage"")?.FirstOrDefault();
                if (img != null) {{ <div class=""block-prop""><strong>Image:</strong><br/><img src=""@img.MediaUrl()"" alt=""@img.Name"" class=""block-image"" /></div> }}
            }}
            @if (content.HasValue(""linkedContent""))
            {{
                var linked = content.Value<IPublishedContent>(""linkedContent"");
                if (linked != null) {{ <div class=""block-prop""><strong>Linked:</strong> <a href=""@linked.Url()"">@linked.Name</a></div> }}
            }}
            @if (content.HasValue(""nestedBlocks""))
            {{
                var nested = content.Value<BlockListModel>(""nestedBlocks"");
                if (nested != null && nested.Any())
                {{
                    <div class=""nested-blocks"">
                        <strong>Nested Blocks:</strong>
                        @foreach (var nb in nested) {{ RenderBlockContent(nb.Content, depth + 1); }}
                    </div>
                }}
            }}
        </div>
    }}
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
        .block-content {{ padding: 10px; margin: 5px 0; background: #fafafa; border: 1px solid #e0e0e0; }}
        .block-header {{ font-weight: bold; color: #007bff; margin-bottom: 8px; }}
        .block-prop {{ margin: 4px 0; }}
        .block-image {{ max-width: 200px; margin-top: 5px; }}
        .nested-blocks {{ margin-top: 10px; padding-left: 10px; border-left: 2px solid #007bff; }}
        .content-link {{ color: #007bff; text-decoration: none; }}
        .content-link:hover {{ text-decoration: underline; }}
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
        @if (Model.HasValue(""summary"")) {{ <div class=""property""><span class=""property-label"">Summary:</span> @Model.Value(""summary"")</div> }}
    </section>
    @if (Model.HasValue(""relatedContent""))
    {{
        <section>
            <h2>Related Content</h2>
            @{{
                var related = Model.Value<IPublishedContent>(""relatedContent"");
                if (related != null)
                {{
                    <div class=""property"">
                        <a href=""@related.Url()"" class=""content-link"">@related.Name</a>
                    </div>
                }}
            }}
        </section>
    }}
    @if (Model.HasValue(""blocks""))
    {{
        var blocks = Model.Value<BlockListModel>(""blocks"");
        if (blocks != null && blocks.Any())
        {{
            <section>
                <h2>Blocks</h2>
                @foreach (var block in blocks)
                {{
                    <div class=""block-item"">
                        @{{ RenderBlockContent(block.Content); }}
                    </div>
                }}
            </section>
        }}
    }}
    <footer><p>Generated by PerformanceTestDataSeeder</p></footer>
</body>
</html>";

    private static string GenerateComplexTemplateContent(string alias, string name) =>
        $@"@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Umbraco.Cms.Core.Models.Blocks
@using Umbraco.Cms.Core.Models
@using Umbraco.Cms.Core.Models.PublishedContent
@{{
    Layout = null;

    void RenderBlockContent(IPublishedElement content, int depth = 0)
    {{
        <div class=""block-content"" style=""margin-left: @(depth * 20)px"">
            <div class=""block-header"">@content.ContentType.Alias</div>
            @if (content.HasValue(""title"")) {{ <div class=""block-prop""><strong>Title:</strong> @content.Value(""title"")</div> }}
            @if (content.HasValue(""subtitle"")) {{ <div class=""block-prop""><strong>Subtitle:</strong> @content.Value(""subtitle"")</div> }}
            @if (content.HasValue(""description"")) {{ <div class=""block-prop""><strong>Description:</strong> @content.Value(""description"")</div> }}
            @if (content.HasValue(""summary"")) {{ <div class=""block-prop""><strong>Summary:</strong> @content.Value(""summary"")</div> }}
            @if (content.HasValue(""containerTitle"")) {{ <div class=""block-prop""><strong>Container:</strong> @content.Value(""containerTitle"")</div> }}
            @if (content.HasValue(""isActive"")) {{ <div class=""block-prop""><strong>Active:</strong> @content.Value(""isActive"")</div> }}
            @if (content.HasValue(""isVisible"")) {{ <div class=""block-prop""><strong>Visible:</strong> @content.Value(""isVisible"")</div> }}
            @if (content.HasValue(""isEnabled"")) {{ <div class=""block-prop""><strong>Enabled:</strong> @content.Value(""isEnabled"")</div> }}
            @if (content.HasValue(""cssClass"")) {{ <div class=""block-prop""><strong>CSS Class:</strong> @content.Value(""cssClass"")</div> }}
            @if (content.HasValue(""metaTitle"")) {{ <div class=""block-prop""><strong>Meta Title:</strong> @content.Value(""metaTitle"")</div> }}
            @if (content.HasValue(""metaDescription"")) {{ <div class=""block-prop""><strong>Meta Description:</strong> @content.Value(""metaDescription"")</div> }}
            @if (content.HasValue(""mainImage""))
            {{
                var img = content.Value<IEnumerable<MediaWithCrops>>(""mainImage"")?.FirstOrDefault();
                if (img != null) {{ <div class=""block-prop""><strong>Main Image:</strong><br/><img src=""@img.MediaUrl()"" alt=""@img.Name"" class=""block-image"" /></div> }}
            }}
            @if (content.HasValue(""thumbnailImage""))
            {{
                var thumb = content.Value<IEnumerable<MediaWithCrops>>(""thumbnailImage"")?.FirstOrDefault();
                if (thumb != null) {{ <div class=""block-prop""><strong>Thumbnail:</strong><br/><img src=""@thumb.MediaUrl()"" alt=""@thumb.Name"" class=""block-thumbnail"" /></div> }}
            }}
            @if (content.HasValue(""linkedContent""))
            {{
                var linked = content.Value<IPublishedContent>(""linkedContent"");
                if (linked != null) {{ <div class=""block-prop""><strong>Linked:</strong> <a href=""@linked.Url()"" class=""content-link"">@linked.Name</a></div> }}
            }}
            @if (content.HasValue(""primaryLink""))
            {{
                var primary = content.Value<IPublishedContent>(""primaryLink"");
                if (primary != null) {{ <div class=""block-prop""><strong>Primary Link:</strong> <a href=""@primary.Url()"" class=""content-link"">@primary.Name</a></div> }}
            }}
            @if (content.HasValue(""secondaryLink""))
            {{
                var secondary = content.Value<IPublishedContent>(""secondaryLink"");
                if (secondary != null) {{ <div class=""block-prop""><strong>Secondary Link:</strong> <a href=""@secondary.Url()"" class=""content-link"">@secondary.Name</a></div> }}
            }}
            @if (content.HasValue(""nestedBlocks""))
            {{
                var nested = content.Value<BlockListModel>(""nestedBlocks"");
                if (nested != null && nested.Any())
                {{
                    <div class=""nested-blocks"">
                        <strong>Nested Blocks:</strong>
                        @foreach (var nb in nested) {{ RenderBlockContent(nb.Content, depth + 1); }}
                    </div>
                }}
            }}
        </div>
    }}

    void RenderGridItem(BlockGridItem item, int depth = 0)
    {{
        <div class=""grid-item"" style=""margin-left: @(depth * 20)px"">
            <div class=""grid-header"">@item.Content.ContentType.Alias <span class=""grid-span"">(Columns: @item.ColumnSpan, Row: @item.RowSpan)</span></div>
            @{{ RenderBlockContent(item.Content, 0); }}
            @if (item.Areas != null && item.Areas.Any())
            {{
                foreach (var area in item.Areas)
                {{
                    if (area.Any())
                    {{
                        <div class=""grid-area"">
                            <strong>Area: @area.Alias</strong>
                            @foreach (var areaItem in area) {{ RenderGridItem(areaItem, depth + 1); }}
                        </div>
                    }}
                }}
            }}
        </div>
    }}
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
        .block-content {{ padding: 10px; margin: 5px 0; background: #fafafa; border: 1px solid #e0e0e0; }}
        .block-header {{ font-weight: bold; color: #007bff; margin-bottom: 8px; }}
        .block-prop {{ margin: 4px 0; }}
        .block-image {{ max-width: 200px; margin-top: 5px; }}
        .block-thumbnail {{ max-width: 100px; margin-top: 5px; }}
        .nested-blocks {{ margin-top: 10px; padding-left: 10px; border-left: 2px solid #007bff; }}
        .grid-item {{ margin: 10px 0; padding: 15px; background: #e8f4e8; border-left: 3px solid #28a745; }}
        .grid-header {{ font-weight: bold; color: #28a745; margin-bottom: 8px; }}
        .grid-span {{ font-weight: normal; color: #666; font-size: 0.9em; }}
        .grid-area {{ margin-top: 10px; padding: 10px; background: #f0f8f0; border: 1px dashed #28a745; }}
        .media-image {{ max-width: 300px; }}
        .media-thumbnail {{ max-width: 150px; margin-left: 20px; }}
        .content-link {{ color: #007bff; text-decoration: none; }}
        .content-link:hover {{ text-decoration: underline; }}
        .related-list {{ list-style: none; padding: 0; }}
        .related-list li {{ margin: 8px 0; padding: 8px; background: #f8f9fa; border-radius: 4px; }}
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
            var img = Model.Value<IEnumerable<MediaWithCrops>>(""mainImage"")?.FirstOrDefault();
            if (img != null) {{ <img src=""@img.MediaUrl()"" alt=""@img.Name"" class=""media-image"" /> }}
        }}
        @if (Model.HasValue(""thumbnailImage""))
        {{
            var thumb = Model.Value<IEnumerable<MediaWithCrops>>(""thumbnailImage"")?.FirstOrDefault();
            if (thumb != null) {{ <img src=""@thumb.MediaUrl()"" alt=""@thumb.Name"" class=""media-thumbnail"" /> }}
        }}
    </section>
    @if (Model.HasValue(""primaryContent"") || Model.HasValue(""secondaryContent"") || Model.HasValue(""tertiaryContent""))
    {{
        <section>
            <h2>Related Content</h2>
            <ul class=""related-list"">
            @if (Model.HasValue(""primaryContent""))
            {{
                var primary = Model.Value<IPublishedContent>(""primaryContent"");
                if (primary != null) {{ <li><strong>Primary:</strong> <a href=""@primary.Url()"" class=""content-link"">@primary.Name</a></li> }}
            }}
            @if (Model.HasValue(""secondaryContent""))
            {{
                var secondary = Model.Value<IPublishedContent>(""secondaryContent"");
                if (secondary != null) {{ <li><strong>Secondary:</strong> <a href=""@secondary.Url()"" class=""content-link"">@secondary.Name</a></li> }}
            }}
            @if (Model.HasValue(""tertiaryContent""))
            {{
                var tertiary = Model.Value<IPublishedContent>(""tertiaryContent"");
                if (tertiary != null) {{ <li><strong>Tertiary:</strong> <a href=""@tertiary.Url()"" class=""content-link"">@tertiary.Name</a></li> }}
            }}
            </ul>
        </section>
    }}
    @if (Model.HasValue(""headerBlocks""))
    {{
        <section>
            <h2>Header Blocks</h2>
            @{{
                var headerBlocks = Model.Value<BlockListModel>(""headerBlocks"");
                if (headerBlocks != null)
                {{
                    foreach (var block in headerBlocks)
                    {{
                        <div class=""block-item"">
                            @{{ RenderBlockContent(block.Content); }}
                        </div>
                    }}
                }}
            }}
        </section>
    }}
    @if (Model.HasValue(""footerBlocks""))
    {{
        <section>
            <h2>Footer Blocks</h2>
            @{{
                var footerBlocks = Model.Value<BlockListModel>(""footerBlocks"");
                if (footerBlocks != null)
                {{
                    foreach (var block in footerBlocks)
                    {{
                        <div class=""block-item"">
                            @{{ RenderBlockContent(block.Content); }}
                        </div>
                    }}
                }}
            }}
        </section>
    }}
    @if (Model.HasValue(""mainGrid""))
    {{
        <section>
            <h2>Main Grid</h2>
            @{{
                var grid = Model.Value<BlockGridModel>(""mainGrid"");
                if (grid != null)
                {{
                    foreach (var gridItem in grid)
                    {{
                        RenderGridItem(gridItem);
                    }}
                }}
            }}
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
            SortOrder = 3,
            Variations = docType.Variations
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
            { Alias = "relatedContent", Name = "Related Content", SortOrder = 1, Variations = docType.Variations });
        }
        if (blockListDataType != null)
        {
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
            { Alias = "blocks", Name = "Blocks", SortOrder = 2, Variations = docType.Variations });
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
            { Alias = "mainImage", Name = "Main Image", SortOrder = 1, Variations = docType.Variations });
            mediaGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.MediaPickerDataType)
            { Alias = "thumbnailImage", Name = "Thumbnail Image", SortOrder = 2, Variations = docType.Variations });
        }

        var relationsGroup = new PropertyGroup(true) { Alias = "relations", Name = "Relations", SortOrder = 3 };
        if (Context.ContentPickerDataType != null)
        {
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "primaryContent", Name = "Primary Content", SortOrder = 1, Variations = docType.Variations });
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "secondaryContent", Name = "Secondary Content", SortOrder = 2, Variations = docType.Variations });
            relationsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            { Alias = "tertiaryContent", Name = "Tertiary Content", SortOrder = 3, Variations = docType.Variations });
        }

        var blocksGroup = new PropertyGroup(true) { Alias = "blocks", Name = "Blocks", SortOrder = 4 };
        if (blockListDataType != null)
        {
            blocksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
            { Alias = "headerBlocks", Name = "Header Blocks", SortOrder = 1, Variations = docType.Variations });
            blocksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
            { Alias = "footerBlocks", Name = "Footer Blocks", SortOrder = 2, Variations = docType.Variations });
        }
        if (blockGridDataType != null)
        {
            blocksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockGridDataType)
            { Alias = "mainGrid", Name = "Main Grid", SortOrder = 3, Variations = docType.Variations });
        }

        docType.PropertyGroups.Add(contentGroup);
        docType.PropertyGroups.Add(mediaGroup);
        docType.PropertyGroups.Add(relationsGroup);
        docType.PropertyGroups.Add(blocksGroup);
    }

    #endregion
}
