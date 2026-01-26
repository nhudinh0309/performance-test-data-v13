namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Bogus;
using Umbraco.Cms.Core.Models;

/// <summary>
/// Shared execution context for all seeders. Provides caching and shared resources
/// to avoid repeated lookups and improve performance.
/// </summary>
public class SeederExecutionContext
{
    /// <summary>
    /// Shared Faker instance for generating test data.
    /// Uses a consistent seed for reproducible data when configured.
    /// </summary>
    public Faker Faker { get; }

    /// <summary>
    /// Shared Random instance for random selections.
    /// Uses a consistent seed for reproducible data when configured.
    /// </summary>
    public Random Random { get; }

    /// <summary>
    /// The seed used for Faker and Random (for debugging/logging).
    /// </summary>
    public int Seed { get; }

    // === Caches populated by seeders ===

    /// <summary>
    /// Languages created by LanguageSeeder (used by DictionarySeeder, ContentSeeder).
    /// </summary>
    public List<ILanguage> Languages { get; set; } = new();

    /// <summary>
    /// Element types created by DocumentTypeSeeder (used by ContentSeeder).
    /// </summary>
    public List<IContentType> ElementTypes { get; set; } = new();

    /// <summary>
    /// Simple document types created by DocumentTypeSeeder.
    /// </summary>
    public List<IContentType> SimpleDocTypes { get; set; } = new();

    /// <summary>
    /// Medium document types created by DocumentTypeSeeder.
    /// </summary>
    public List<IContentType> MediumDocTypes { get; set; } = new();

    /// <summary>
    /// Complex document types created by DocumentTypeSeeder.
    /// </summary>
    public List<IContentType> ComplexDocTypes { get; set; } = new();

    /// <summary>
    /// Block List data types created by DocumentTypeSeeder.
    /// </summary>
    public List<IDataType> BlockListDataTypes { get; set; } = new();

    /// <summary>
    /// Block Grid data types created by DocumentTypeSeeder.
    /// </summary>
    public List<IDataType> BlockGridDataTypes { get; set; } = new();

    /// <summary>
    /// Media items created by MediaSeeder (used by ContentSeeder).
    /// </summary>
    public List<IMedia> MediaItems { get; set; } = new();

    /// <summary>
    /// Content items created by ContentSeeder (used for linking).
    /// </summary>
    public List<IContent> CreatedContent { get; set; } = new();

    // === Built-in data type cache ===

    /// <summary>
    /// Built-in Textstring data type.
    /// </summary>
    public IDataType? TextstringDataType { get; set; }

    /// <summary>
    /// Built-in Textarea data type.
    /// </summary>
    public IDataType? TextareaDataType { get; set; }

    /// <summary>
    /// Built-in True/False data type.
    /// </summary>
    public IDataType? TrueFalseDataType { get; set; }

    /// <summary>
    /// Built-in Label data type.
    /// </summary>
    public IDataType? LabelDataType { get; set; }

    /// <summary>
    /// Built-in Content Picker data type.
    /// </summary>
    public IDataType? ContentPickerDataType { get; set; }

    /// <summary>
    /// Built-in Media Picker data type.
    /// </summary>
    public IDataType? MediaPickerDataType { get; set; }

    /// <summary>
    /// Built-in Upload data type.
    /// </summary>
    public IDataType? UploadDataType { get; set; }

    /// <summary>
    /// Built-in Numeric data type.
    /// </summary>
    public IDataType? NumericDataType { get; set; }

    /// <summary>
    /// Built-in DateTime data type.
    /// </summary>
    public IDataType? DateTimeDataType { get; set; }

    /// <summary>
    /// Cache of data types by ID for quick lookups.
    /// </summary>
    public Dictionary<int, IDataType> DataTypeCache { get; } = new();

    /// <summary>
    /// Creates a new SeederExecutionContext with optional seed for reproducibility.
    /// </summary>
    /// <param name="fakerSeed">Optional seed for deterministic data generation. If null, uses current time.</param>
    public SeederExecutionContext(int? fakerSeed = null)
    {
        Seed = fakerSeed ?? Environment.TickCount;
        Faker = new Faker("en") { Random = new Randomizer(Seed) };
        Random = new Random(Seed);
    }
}
