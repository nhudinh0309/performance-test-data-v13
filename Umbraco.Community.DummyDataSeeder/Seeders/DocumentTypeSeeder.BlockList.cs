namespace Umbraco.Community.DummyDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;

/// <summary>
/// Partial class containing Block List data type creation logic.
/// </summary>
public partial class DocumentTypeSeeder
{
    private List<IDataType> CreateBlockListDataTypes(int count, CancellationToken cancellationToken)
    {
        var blockListDataTypes = new List<IDataType>();
        var editor = _propertyEditors[Constants.PropertyEditors.Aliases.BlockList];
        if (editor == null)
        {
            Logger.LogWarning("BlockList property editor not found");
            return blockListDataTypes;
        }

        var prefix = GetPrefix("datatype");

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var name = $"{prefix}BlockList_{i + 1}";
                var dataType = new DataType(editor, _serializer)
                {
                    Name = name,
                    DatabaseType = ValueStorageType.Ntext
                };

                var blocks = new List<BlockListConfiguration.BlockConfiguration>();

                // Add one block of each complexity (using pre-cached lists)
                if (_cachedSimpleElements?.Count > 0)
                {
                    var simpleElement = _cachedSimpleElements[i % _cachedSimpleElements.Count];
                    blocks.Add(new BlockListConfiguration.BlockConfiguration
                    {
                        ContentElementTypeKey = simpleElement.Key,
                        Label = simpleElement.Name
                    });
                }

                if (_cachedMediumElements?.Count > 0)
                {
                    var mediumElement = _cachedMediumElements[i % _cachedMediumElements.Count];
                    blocks.Add(new BlockListConfiguration.BlockConfiguration
                    {
                        ContentElementTypeKey = mediumElement.Key,
                        Label = mediumElement.Name
                    });
                }

                if (_cachedComplexElements?.Count > 0)
                {
                    var complexElement = _cachedComplexElements[i % _cachedComplexElements.Count];
                    blocks.Add(new BlockListConfiguration.BlockConfiguration
                    {
                        ContentElementTypeKey = complexElement.Key,
                        Label = complexElement.Name
                    });
                }

                dataType.Configuration = new BlockListConfiguration { Blocks = blocks.ToArray() };

                _dataTypeService.Save(dataType);
                blockListDataTypes.Add(dataType);

                LogProgress(i + 1, count, "Block List data types");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create Block List data type {Index}", i + 1);
                if (Options.StopOnError) throw;
            }
        }

        return blockListDataTypes;
    }
}
