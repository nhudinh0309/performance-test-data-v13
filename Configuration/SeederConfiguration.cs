namespace Umbraco.Community.PerformanceTestDataSeeder.Configuration;

using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Configuration model for seeder target counts.
/// Override values in appsettings.json under "SeederConfiguration" section.
/// </summary>
public class SeederConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "SeederConfiguration";

    /// <summary>
    /// Language seeding configuration.
    /// </summary>
    public LanguagesConfig Languages { get; set; } = new();

    /// <summary>
    /// Dictionary item seeding configuration.
    /// </summary>
    public DictionaryConfig Dictionary { get; set; } = new();

    /// <summary>
    /// User seeding configuration.
    /// </summary>
    public UsersConfig Users { get; set; } = new();

    /// <summary>
    /// Data type seeding configuration.
    /// </summary>
    public DataTypesConfig DataTypes { get; set; } = new();

    /// <summary>
    /// Document type seeding configuration.
    /// </summary>
    public DocumentTypesConfig DocumentTypes { get; set; } = new();

    /// <summary>
    /// Media seeding configuration.
    /// </summary>
    public MediaConfig Media { get; set; } = new();

    /// <summary>
    /// Content seeding configuration.
    /// </summary>
    public ContentConfig Content { get; set; } = new();
}

/// <summary>
/// Language seeding configuration.
/// </summary>
public class LanguagesConfig
{
    /// <summary>
    /// Number of languages to seed (max 30).
    /// </summary>
    public int Count { get; set; } = 20;
}

/// <summary>
/// Dictionary item seeding configuration.
/// </summary>
public class DictionaryConfig
{
    /// <summary>
    /// Number of root dictionary folders.
    /// </summary>
    public int RootFolders { get; set; } = 10;

    /// <summary>
    /// Number of sections per root folder.
    /// </summary>
    public int SectionsPerRoot { get; set; } = 5;

    /// <summary>
    /// Number of dictionary items per section.
    /// </summary>
    public int ItemsPerSection { get; set; } = 30;

    /// <summary>
    /// Total dictionary items to be created.
    /// </summary>
    public int TotalItems => RootFolders * SectionsPerRoot * ItemsPerSection;
}

/// <summary>
/// User seeding configuration.
/// </summary>
public class UsersConfig
{
    /// <summary>
    /// Number of test users to seed.
    /// </summary>
    public int Count { get; set; } = 30;
}

/// <summary>
/// Data type seeding configuration.
/// </summary>
public class DataTypesConfig
{
    /// <summary>
    /// Number of ListView data types.
    /// </summary>
    public int ListView { get; set; } = 30;

    /// <summary>
    /// Number of MultiNodeTreePicker data types.
    /// </summary>
    public int MultiNodeTreePicker { get; set; } = 40;

    /// <summary>
    /// Number of RichTextEditor data types.
    /// </summary>
    public int RichTextEditor { get; set; } = 10;

    /// <summary>
    /// Number of MediaPicker data types.
    /// </summary>
    public int MediaPicker { get; set; } = 10;

    /// <summary>
    /// Number of Textarea data types.
    /// </summary>
    public int Textarea { get; set; } = 10;

    /// <summary>
    /// Number of Dropdown data types.
    /// </summary>
    public int Dropdown { get; set; } = 10;

    /// <summary>
    /// Number of Numeric data types.
    /// </summary>
    public int Numeric { get; set; } = 10;

    /// <summary>
    /// Total data types to be created.
    /// </summary>
    public int Total => ListView + MultiNodeTreePicker + RichTextEditor + MediaPicker + Textarea + Dropdown + Numeric;
}

/// <summary>
/// Document type seeding configuration.
/// </summary>
public class DocumentTypesConfig
{
    /// <summary>
    /// Element type counts by complexity.
    /// </summary>
    public ComplexityConfig ElementTypes { get; set; } = new() { Simple = 65, Medium = 39, Complex = 26 };

    /// <summary>
    /// Variant document type counts by complexity.
    /// </summary>
    public ComplexityConfig VariantDocTypes { get; set; } = new() { Simple = 55, Medium = 33, Complex = 22 };

    /// <summary>
    /// Invariant document type counts by complexity.
    /// </summary>
    public ComplexityConfig InvariantDocTypes { get; set; } = new() { Simple = 20, Medium = 12, Complex = 8 };

    /// <summary>
    /// Number of Block List data types.
    /// </summary>
    public int BlockList { get; set; } = 40;

    /// <summary>
    /// Number of Block Grid data types.
    /// </summary>
    public int BlockGrid { get; set; } = 60;

    /// <summary>
    /// Depth of nested blocks (blocks containing BlockList properties with other blocks).
    /// Minimum 1 (no nesting), recommended 2-8 depending on load test requirements.
    /// </summary>
    public int NestingDepth { get; set; } = 2;

    /// <summary>
    /// Maximum number of block elements to add to each Block List or Block Grid.
    /// Higher values create more complex block editors but may impact editor performance.
    /// Default: 30
    /// </summary>
    public int MaxBlocksPerEditor { get; set; } = SeederConstants.DefaultMaxBlocksPerEditor;

    /// <summary>
    /// Total element types to be created.
    /// </summary>
    public int TotalElementTypes => ElementTypes.Total;

    /// <summary>
    /// Total document types to be created.
    /// </summary>
    public int TotalDocTypes => VariantDocTypes.Total + InvariantDocTypes.Total;

    /// <summary>
    /// Total templates to be created (one per document type).
    /// </summary>
    public int TotalTemplates => TotalDocTypes;
}

/// <summary>
/// Complexity distribution configuration.
/// </summary>
public class ComplexityConfig
{
    /// <summary>
    /// Count of simple items.
    /// </summary>
    public int Simple { get; set; }

    /// <summary>
    /// Count of medium complexity items.
    /// </summary>
    public int Medium { get; set; }

    /// <summary>
    /// Count of complex items.
    /// </summary>
    public int Complex { get; set; }

    /// <summary>
    /// Total items across all complexity levels.
    /// </summary>
    public int Total => Simple + Medium + Complex;
}

/// <summary>
/// Media seeding configuration.
/// </summary>
public class MediaConfig
{
    /// <summary>
    /// PDF media configuration.
    /// </summary>
    public MediaTypeConfig PDF { get; set; } = new() { Count = 200, FolderCount = 10 };

    /// <summary>
    /// PNG image configuration.
    /// </summary>
    public MediaTypeConfig PNG { get; set; } = new() { Count = 200, FolderCount = 10 };

    /// <summary>
    /// JPG image configuration.
    /// </summary>
    public MediaTypeConfig JPG { get; set; } = new() { Count = 200, FolderCount = 10 };

    /// <summary>
    /// Video media configuration.
    /// </summary>
    public MediaTypeConfig Video { get; set; } = new() { Count = 10, FolderCount = 1 };

    /// <summary>
    /// Total media items to be created.
    /// </summary>
    public int TotalCount => PDF.Count + PNG.Count + JPG.Count + Video.Count;
}

/// <summary>
/// Configuration for a single media type.
/// </summary>
public class MediaTypeConfig
{
    /// <summary>
    /// Number of media items to create.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Number of folders to distribute items into.
    /// </summary>
    public int FolderCount { get; set; } = 1;
}

/// <summary>
/// Content seeding configuration.
/// </summary>
public class ContentConfig
{
    /// <summary>
    /// Total content nodes to create.
    /// </summary>
    public int TotalTarget { get; set; } = 300;

    /// <summary>
    /// Percentage of simple content (must sum to 100 with Medium and Complex).
    /// </summary>
    public int SimplePercent { get; set; } = 50;

    /// <summary>
    /// Percentage of medium content (must sum to 100 with Simple and Complex).
    /// </summary>
    public int MediumPercent { get; set; } = 30;

    /// <summary>
    /// Percentage of complex content (must sum to 100 with Simple and Medium).
    /// </summary>
    public int ComplexPercent { get; set; } = 20;

    /// <summary>
    /// Number of root sections in content tree.
    /// </summary>
    public int RootSections { get; set; } = 50;

    /// <summary>
    /// Number of categories per root section.
    /// </summary>
    public int CategoriesPerSection { get; set; } = 10;

    /// <summary>
    /// Number of pages per category.
    /// </summary>
    public int PagesPerCategory { get; set; } = 100;

    /// <summary>
    /// Target number of simple content items.
    /// </summary>
    public int SimpleTarget => TotalTarget * SimplePercent / 100;

    /// <summary>
    /// Target number of medium content items.
    /// </summary>
    public int MediumTarget => TotalTarget * MediumPercent / 100;

    /// <summary>
    /// Target number of complex content items.
    /// </summary>
    public int ComplexTarget => TotalTarget * ComplexPercent / 100;
}
