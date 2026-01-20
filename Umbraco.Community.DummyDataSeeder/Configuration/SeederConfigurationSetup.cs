namespace Umbraco.Community.DummyDataSeeder.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
/// Post-configures SeederConfiguration to apply preset values when a preset is selected.
/// </summary>
public class SeederConfigurationSetup : IConfigureOptions<SeederConfiguration>
{
    private readonly SeederOptions _options;

    /// <summary>
    /// Creates a new SeederConfigurationSetup instance.
    /// </summary>
    /// <param name="options">The seeder options containing the preset selection.</param>
    public SeederConfigurationSetup(IOptions<SeederOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Configures the SeederConfiguration by applying preset values if a preset is selected.
    /// </summary>
    /// <param name="config">The configuration to modify.</param>
    public void Configure(SeederConfiguration config)
    {
        // If a preset is selected, override with preset configuration
        if (_options.Preset != SeederPreset.Custom)
        {
            var presetConfig = SeederPresetConfigurations.GetConfiguration(_options.Preset);
            if (presetConfig != null)
            {
                ApplyPreset(config, presetConfig);
            }
        }
    }

    private static void ApplyPreset(SeederConfiguration target, SeederConfiguration source)
    {
        // Languages
        target.Languages.Count = source.Languages.Count;

        // Dictionary
        target.Dictionary.RootFolders = source.Dictionary.RootFolders;
        target.Dictionary.SectionsPerRoot = source.Dictionary.SectionsPerRoot;
        target.Dictionary.ItemsPerSection = source.Dictionary.ItemsPerSection;

        // Users
        target.Users.Count = source.Users.Count;

        // DataTypes
        target.DataTypes.ListView = source.DataTypes.ListView;
        target.DataTypes.MultiNodeTreePicker = source.DataTypes.MultiNodeTreePicker;
        target.DataTypes.RichTextEditor = source.DataTypes.RichTextEditor;
        target.DataTypes.MediaPicker = source.DataTypes.MediaPicker;
        target.DataTypes.Textarea = source.DataTypes.Textarea;
        target.DataTypes.Dropdown = source.DataTypes.Dropdown;
        target.DataTypes.Numeric = source.DataTypes.Numeric;

        // DocumentTypes - ElementTypes
        target.DocumentTypes.ElementTypes.Simple = source.DocumentTypes.ElementTypes.Simple;
        target.DocumentTypes.ElementTypes.Medium = source.DocumentTypes.ElementTypes.Medium;
        target.DocumentTypes.ElementTypes.Complex = source.DocumentTypes.ElementTypes.Complex;

        // DocumentTypes - VariantDocTypes
        target.DocumentTypes.VariantDocTypes.Simple = source.DocumentTypes.VariantDocTypes.Simple;
        target.DocumentTypes.VariantDocTypes.Medium = source.DocumentTypes.VariantDocTypes.Medium;
        target.DocumentTypes.VariantDocTypes.Complex = source.DocumentTypes.VariantDocTypes.Complex;

        // DocumentTypes - InvariantDocTypes
        target.DocumentTypes.InvariantDocTypes.Simple = source.DocumentTypes.InvariantDocTypes.Simple;
        target.DocumentTypes.InvariantDocTypes.Medium = source.DocumentTypes.InvariantDocTypes.Medium;
        target.DocumentTypes.InvariantDocTypes.Complex = source.DocumentTypes.InvariantDocTypes.Complex;

        // DocumentTypes - BlockList/BlockGrid
        target.DocumentTypes.BlockList = source.DocumentTypes.BlockList;
        target.DocumentTypes.BlockGrid = source.DocumentTypes.BlockGrid;

        // Media
        target.Media.PDF.Count = source.Media.PDF.Count;
        target.Media.PDF.FolderCount = source.Media.PDF.FolderCount;
        target.Media.PNG.Count = source.Media.PNG.Count;
        target.Media.PNG.FolderCount = source.Media.PNG.FolderCount;
        target.Media.JPG.Count = source.Media.JPG.Count;
        target.Media.JPG.FolderCount = source.Media.JPG.FolderCount;
        target.Media.Video.Count = source.Media.Video.Count;
        target.Media.Video.FolderCount = source.Media.Video.FolderCount;

        // Content
        target.Content.TotalTarget = source.Content.TotalTarget;
        target.Content.SimplePercent = source.Content.SimplePercent;
        target.Content.MediumPercent = source.Content.MediumPercent;
        target.Content.ComplexPercent = source.Content.ComplexPercent;
        target.Content.RootSections = source.Content.RootSections;
        target.Content.CategoriesPerSection = source.Content.CategoriesPerSection;
        target.Content.PagesPerCategory = source.Content.PagesPerCategory;
    }
}
