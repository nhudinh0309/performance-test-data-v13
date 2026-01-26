namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;

/// <summary>
/// Partial class containing Block Grid data type creation logic.
/// </summary>
public partial class DocumentTypeSeeder
{
    private List<IDataType> CreateBlockGridDataTypes(int count, CancellationToken cancellationToken)
    {
        var blockGridDataTypes = new List<IDataType>();
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockGrid];
        if (editor == null)
        {
            Logger.LogWarning("BlockGrid property editor not found");
            return blockGridDataTypes;
        }

        var prefix = GetPrefix("datatype");

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}BlockGrid_{i + 1}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    DatabaseType = ValueStorageType.Ntext
                };

                // Add up to 30 blocks (cycling through element types)
                var blocks = new List<BlockGridConfiguration.BlockGridBlockConfiguration>();

                // Add level 1 container elements first (if nested containers exist)
                if (_nestedContainersByLevel.TryGetValue(1, out var level1Containers))
                {
                    foreach (var container in level1Containers)
                    {
                        blocks.Add(new BlockGridConfiguration.BlockGridBlockConfiguration
                        {
                            ContentElementTypeKey = container.Key,
                            Label = container.Name,
                            AllowAtRoot = true,
                            AllowInAreas = true
                        });
                    }
                }

                // Add remaining element types up to 30 total
                for (int j = 0; j < 30 - blocks.Count && j < Context.ElementTypes.Count; j++)
                {
                    var elementType = Context.ElementTypes[j % Context.ElementTypes.Count];
                    // Skip if already added as container
                    if (blocks.Any(b => b.ContentElementTypeKey == elementType.Key)) continue;

                    blocks.Add(new BlockGridConfiguration.BlockGridBlockConfiguration
                    {
                        ContentElementTypeKey = elementType.Key,
                        Label = elementType.Name,
                        AllowAtRoot = true,
                        AllowInAreas = true
                    });
                }

                dataType.Configuration = new BlockGridConfiguration
                {
                    Blocks = blocks.ToArray(),
                    GridColumns = 12
                };

                _dataTypeService.Save(dataType);
                blockGridDataTypes.Add(dataType);

                LogProgress(i + 1, count, "Block Grid data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Block Grid data type {Index}", i + 1);
                if (Options.StopOnError) throw;
            }
        }

        return blockGridDataTypes;
    }
}
