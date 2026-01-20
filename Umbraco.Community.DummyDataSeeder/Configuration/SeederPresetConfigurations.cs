namespace Umbraco.Community.DummyDataSeeder.Configuration;

/// <summary>
/// Provides predefined SeederConfiguration values for each preset.
/// </summary>
public static class SeederPresetConfigurations
{
    /// <summary>
    /// Gets the SeederConfiguration for the specified preset.
    /// Returns null for Custom preset (use the user-provided configuration).
    /// </summary>
    public static SeederConfiguration? GetConfiguration(SeederPreset preset)
    {
        return preset switch
        {
            SeederPreset.Custom => null,
            SeederPreset.Small => GetSmallPreset(),
            SeederPreset.Medium => GetMediumPreset(),
            SeederPreset.Large => GetLargePreset(),
            SeederPreset.Massive => GetMassivePreset(),
            _ => null
        };
    }

    /// <summary>
    /// Small dataset for quick testing (~200 items total).
    /// Languages: 3, Content: 50, Media: 30, Users: 5
    /// </summary>
    private static SeederConfiguration GetSmallPreset()
    {
        return new SeederConfiguration
        {
            Languages = new LanguagesConfig { Count = 3 },
            Dictionary = new DictionaryConfig
            {
                RootFolders = 2,
                SectionsPerRoot = 2,
                ItemsPerSection = 5
            },
            Users = new UsersConfig { Count = 5 },
            DataTypes = new DataTypesConfig
            {
                ListView = 3,
                MultiNodeTreePicker = 3,
                RichTextEditor = 2,
                MediaPicker = 2,
                Textarea = 2,
                Dropdown = 2,
                Numeric = 2
            },
            DocumentTypes = new DocumentTypesConfig
            {
                ElementTypes = new ComplexityConfig { Simple = 5, Medium = 3, Complex = 2 },
                VariantDocTypes = new ComplexityConfig { Simple = 5, Medium = 3, Complex = 2 },
                InvariantDocTypes = new ComplexityConfig { Simple = 2, Medium = 1, Complex = 1 },
                BlockList = 3,
                BlockGrid = 3
            },
            Media = new MediaConfig
            {
                PDF = new MediaTypeConfig { Count = 8, FolderCount = 1 },
                PNG = new MediaTypeConfig { Count = 10, FolderCount = 1 },
                JPG = new MediaTypeConfig { Count = 10, FolderCount = 1 },
                Video = new MediaTypeConfig { Count = 2, FolderCount = 1 }
            },
            Content = new ContentConfig
            {
                TotalTarget = 50,
                SimplePercent = 50,
                MediumPercent = 30,
                ComplexPercent = 20,
                RootSections = 3,
                CategoriesPerSection = 2,
                PagesPerCategory = 5
            }
        };
    }

    /// <summary>
    /// Medium dataset for moderate testing (~2,000 items total).
    /// Languages: 10, Content: 500, Media: 500, Users: 20
    /// </summary>
    private static SeederConfiguration GetMediumPreset()
    {
        return new SeederConfiguration
        {
            Languages = new LanguagesConfig { Count = 10 },
            Dictionary = new DictionaryConfig
            {
                RootFolders = 5,
                SectionsPerRoot = 4,
                ItemsPerSection = 10
            },
            Users = new UsersConfig { Count = 20 },
            DataTypes = new DataTypesConfig
            {
                ListView = 10,
                MultiNodeTreePicker = 15,
                RichTextEditor = 5,
                MediaPicker = 5,
                Textarea = 5,
                Dropdown = 5,
                Numeric = 5
            },
            DocumentTypes = new DocumentTypesConfig
            {
                ElementTypes = new ComplexityConfig { Simple = 20, Medium = 12, Complex = 8 },
                VariantDocTypes = new ComplexityConfig { Simple = 20, Medium = 12, Complex = 8 },
                InvariantDocTypes = new ComplexityConfig { Simple = 8, Medium = 5, Complex = 3 },
                BlockList = 15,
                BlockGrid = 15
            },
            Media = new MediaConfig
            {
                PDF = new MediaTypeConfig { Count = 100, FolderCount = 5 },
                PNG = new MediaTypeConfig { Count = 150, FolderCount = 5 },
                JPG = new MediaTypeConfig { Count = 150, FolderCount = 5 },
                Video = new MediaTypeConfig { Count = 5, FolderCount = 1 }
            },
            Content = new ContentConfig
            {
                TotalTarget = 500,
                SimplePercent = 50,
                MediumPercent = 30,
                ComplexPercent = 20,
                RootSections = 10,
                CategoriesPerSection = 5,
                PagesPerCategory = 10
            }
        };
    }

    /// <summary>
    /// Large dataset for performance testing (~25,000 items total).
    /// Languages: 20, Content: 10,000, Media: 5,000, Users: 50
    /// </summary>
    private static SeederConfiguration GetLargePreset()
    {
        return new SeederConfiguration
        {
            Languages = new LanguagesConfig { Count = 20 },
            Dictionary = new DictionaryConfig
            {
                RootFolders = 10,
                SectionsPerRoot = 5,
                ItemsPerSection = 30
            },
            Users = new UsersConfig { Count = 50 },
            DataTypes = new DataTypesConfig
            {
                ListView = 30,
                MultiNodeTreePicker = 40,
                RichTextEditor = 10,
                MediaPicker = 10,
                Textarea = 10,
                Dropdown = 10,
                Numeric = 10
            },
            DocumentTypes = new DocumentTypesConfig
            {
                ElementTypes = new ComplexityConfig { Simple = 65, Medium = 39, Complex = 26 },
                VariantDocTypes = new ComplexityConfig { Simple = 55, Medium = 33, Complex = 22 },
                InvariantDocTypes = new ComplexityConfig { Simple = 20, Medium = 12, Complex = 8 },
                BlockList = 40,
                BlockGrid = 60
            },
            Media = new MediaConfig
            {
                PDF = new MediaTypeConfig { Count = 1000, FolderCount = 20 },
                PNG = new MediaTypeConfig { Count = 1500, FolderCount = 20 },
                JPG = new MediaTypeConfig { Count = 1500, FolderCount = 20 },
                Video = new MediaTypeConfig { Count = 20, FolderCount = 2 }
            },
            Content = new ContentConfig
            {
                TotalTarget = 10000,
                SimplePercent = 50,
                MediumPercent = 30,
                ComplexPercent = 20,
                RootSections = 50,
                CategoriesPerSection = 10,
                PagesPerCategory = 20
            }
        };
    }

    /// <summary>
    /// Massive dataset for stress testing (~100,000 items total).
    /// Languages: 30, Content: 50,000, Media: 20,000, Users: 100
    /// </summary>
    private static SeederConfiguration GetMassivePreset()
    {
        return new SeederConfiguration
        {
            Languages = new LanguagesConfig { Count = 30 },
            Dictionary = new DictionaryConfig
            {
                RootFolders = 15,
                SectionsPerRoot = 8,
                ItemsPerSection = 50
            },
            Users = new UsersConfig { Count = 100 },
            DataTypes = new DataTypesConfig
            {
                ListView = 50,
                MultiNodeTreePicker = 60,
                RichTextEditor = 20,
                MediaPicker = 20,
                Textarea = 20,
                Dropdown = 15,
                Numeric = 15
            },
            DocumentTypes = new DocumentTypesConfig
            {
                ElementTypes = new ComplexityConfig { Simple = 100, Medium = 60, Complex = 40 },
                VariantDocTypes = new ComplexityConfig { Simple = 80, Medium = 48, Complex = 32 },
                InvariantDocTypes = new ComplexityConfig { Simple = 30, Medium = 18, Complex = 12 },
                BlockList = 60,
                BlockGrid = 80
            },
            Media = new MediaConfig
            {
                PDF = new MediaTypeConfig { Count = 4000, FolderCount = 50 },
                PNG = new MediaTypeConfig { Count = 6000, FolderCount = 50 },
                JPG = new MediaTypeConfig { Count = 6000, FolderCount = 50 },
                Video = new MediaTypeConfig { Count = 50, FolderCount = 5 }
            },
            Content = new ContentConfig
            {
                TotalTarget = 50000,
                SimplePercent = 50,
                MediumPercent = 30,
                ComplexPercent = 20,
                RootSections = 100,
                CategoriesPerSection = 20,
                PagesPerCategory = 25
            }
        };
    }
}
