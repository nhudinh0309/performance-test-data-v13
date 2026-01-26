namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// Partial class containing Element Type creation logic.
/// </summary>
public partial class DocumentTypeSeeder
{
    /// <summary>
    /// Creates nested container element types with BlockList properties.
    /// Each level contains a BlockList that allows elements from the level below.
    /// Level N (deepest) = leaf elements (Simple/Medium/Complex)
    /// Level 1 (top) = outermost container
    /// </summary>
    private void CreateNestedContainerElements(int nestingDepth, CancellationToken cancellationToken)
    {
        var prefix = GetPrefix("elementtype");
        if (!_propertyEditors.TryGet(Constants.PropertyEditors.Aliases.BlockList, out var editor))
        {
            Logger.LogWarning("BlockList property editor not found - skipping nested containers");
            return;
        }

        // Level N (deepest) = existing leaf elements (Simple/Medium/Complex)
        // We start creating containers from level N-1 down to level 1

        // Initialize the deepest level with existing leaf elements
        _nestedContainersByLevel[nestingDepth] = Context.ElementTypes.ToList();

        for (int level = nestingDepth - 1; level >= 1; level--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elementsForThisLevel = new List<IContentType>();
            var childElements = _nestedContainersByLevel[level + 1];

            // Create a BlockList data type for this nesting level
            var blockListDataType = CreateNestedBlockListDataType(level, childElements);
            if (blockListDataType == null) continue;

            // Create 3 container element types per level (to provide variety)
            for (int i = 1; i <= 3; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var alias = $"{prefix}NestedBlock_Depth{level}_{i}";
                    var name = $"Nested Block Depth {level} - {i}";

                    var containerElement = new ContentType(_shortStringHelper, -1)
                    {
                        Alias = alias,
                        Name = name,
                        IsElement = true,
                        Icon = "icon-layers"
                    };

                    // Add a title property
                    var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
                    contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
                    {
                        Alias = "containerTitle",
                        Name = "Container Title",
                        SortOrder = 1
                    });

                    // Add the nested BlockList property
                    contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, blockListDataType)
                    {
                        Alias = "nestedBlocks",
                        Name = "Nested Blocks",
                        SortOrder = 2
                    });

                    containerElement.PropertyGroups.Add(contentGroup);

                    _contentTypeService.Save(containerElement);
                    elementsForThisLevel.Add(containerElement);
                    Context.ElementTypes.Add(containerElement);

                    Logger.LogDebug("Created nested block element: {Alias} (depth {Level})", alias, level);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to create nested container at level {Level}", level);
                    if (Options.StopOnError) throw;
                }
            }

            _nestedContainersByLevel[level] = elementsForThisLevel;
        }

        Logger.LogInformation("Created nested container elements for {Levels} levels", nestingDepth - 1);
    }

    /// <summary>
    /// Creates a BlockList data type configured to allow the specified child elements.
    /// </summary>
    private IDataType? CreateNestedBlockListDataType(int level, List<IContentType> allowedElements)
    {
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockList];
        if (editor == null) return null;

        var prefix = GetPrefix("datatype");

        try
        {
            var name = $"{prefix}NestedBlockList_Depth{level}";
            var dataType = new DataType(editor, _serializer)
            {
                Name = name,
                DatabaseType = ValueStorageType.Ntext
            };

            var blocks = allowedElements.Select(element => new BlockListConfiguration.BlockConfiguration
            {
                ContentElementTypeKey = element.Key,
                Label = element.Name
            }).ToArray();

            dataType.Configuration = new BlockListConfiguration { Blocks = blocks };

            _dataTypeService.Save(dataType);
            Logger.LogDebug("Created nested BlockList data type: {Name} with {Count} allowed blocks", name, blocks.Length);

            return dataType;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to create nested BlockList data type for level {Level}", level);
            if (Options.StopOnError) throw;
            return null;
        }
    }

    private void CreateElementTypes(ComplexityConfig config, CancellationToken cancellationToken)
    {
        var prefix = GetPrefix("elementtype");
        int totalCreated = 0;

        // Simple (3 properties, 1 tab)
        for (int i = 1; i <= config.Simple; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var elementType = CreateElementType($"{prefix}Simple{i}", $"Test Element Simple {i}", "simple");
                Context.ElementTypes.Add(elementType);
                totalCreated++;
                LogProgress(totalCreated, config.Total, "element types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create simple element type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        // Medium (5 properties, 2 tabs)
        for (int i = 1; i <= config.Medium; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var elementType = CreateElementType($"{prefix}Medium{i}", $"Test Element Medium {i}", "medium");
                Context.ElementTypes.Add(elementType);
                totalCreated++;
                LogProgress(totalCreated, config.Total, "element types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create medium element type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        // Complex (10 properties, 5 tabs)
        for (int i = 1; i <= config.Complex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var elementType = CreateElementType($"{prefix}Complex{i}", $"Test Element Complex {i}", "complex");
                Context.ElementTypes.Add(elementType);
                totalCreated++;
                LogProgress(totalCreated, config.Total, "element types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create complex element type {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Created {Count} element types (target: {Target})", totalCreated, config.Total);
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
                AddSimpleElementProperties(elementType);
                break;
            case "medium":
                AddMediumElementProperties(elementType);
                break;
            case "complex":
                AddComplexElementProperties(elementType);
                break;
        }

        _contentTypeService.Save(elementType);
        return elementType;
    }

    #region Element Type Properties

    private void AddSimpleElementProperties(IContentType contentType)
    {
        var group = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };

        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextareaDataType())
        {
            Alias = "description",
            Name = "Description",
            SortOrder = 2
        });
        group.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTrueFalseDataType())
        {
            Alias = "isActive",
            Name = "Is Active",
            SortOrder = 3
        });

        contentType.PropertyGroups.Add(group);
    }

    private void AddMediumElementProperties(IContentType contentType)
    {
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1
        });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "subtitle",
            Name = "Subtitle",
            SortOrder = 2
        });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextareaDataType())
        {
            Alias = "description",
            Name = "Description",
            SortOrder = 3
        });

        var settingsGroup = new PropertyGroup(true) { Alias = "settings", Name = "Settings", SortOrder = 2 };
        settingsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTrueFalseDataType())
        {
            Alias = "isVisible",
            Name = "Is Visible",
            SortOrder = 1
        });
        if (Context.ContentPickerDataType != null)
        {
            settingsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            {
                Alias = "linkedContent",
                Name = "Linked Content",
                SortOrder = 2
            });
        }

        contentType.PropertyGroups.Add(contentGroup);
        contentType.PropertyGroups.Add(settingsGroup);
    }

    private void AddComplexElementProperties(IContentType contentType)
    {
        var contentGroup = new PropertyGroup(true) { Alias = "content", Name = "Content", SortOrder = 1 };
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "title",
            Name = "Title",
            SortOrder = 1
        });
        contentGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextareaDataType())
        {
            Alias = "summary",
            Name = "Summary",
            SortOrder = 2
        });

        var mediaGroup = new PropertyGroup(true) { Alias = "media", Name = "Media", SortOrder = 2 };
        if (Context.MediaPickerDataType != null)
        {
            mediaGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.MediaPickerDataType)
            {
                Alias = "mainImage",
                Name = "Main Image",
                SortOrder = 1
            });
            mediaGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.MediaPickerDataType)
            {
                Alias = "thumbnailImage",
                Name = "Thumbnail Image",
                SortOrder = 2
            });
        }

        var linksGroup = new PropertyGroup(true) { Alias = "links", Name = "Links", SortOrder = 3 };
        if (Context.ContentPickerDataType != null)
        {
            linksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            {
                Alias = "primaryLink",
                Name = "Primary Link",
                SortOrder = 1
            });
            linksGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, Context.ContentPickerDataType)
            {
                Alias = "secondaryLink",
                Name = "Secondary Link",
                SortOrder = 2
            });
        }

        var settingsGroup = new PropertyGroup(true) { Alias = "settings", Name = "Settings", SortOrder = 4 };
        settingsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTrueFalseDataType())
        {
            Alias = "isEnabled",
            Name = "Is Enabled",
            SortOrder = 1
        });
        settingsGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "cssClass",
            Name = "CSS Class",
            SortOrder = 2
        });

        var seoGroup = new PropertyGroup(true) { Alias = "seo", Name = "SEO", SortOrder = 5 };
        seoGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextstringDataType())
        {
            Alias = "metaTitle",
            Name = "Meta Title",
            SortOrder = 1
        });
        seoGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, GetTextareaDataType())
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
}
