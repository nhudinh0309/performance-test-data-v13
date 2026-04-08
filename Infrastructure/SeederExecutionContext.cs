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
    /// Internal Random instance - use GetNextRandom() for thread-safe access.
    /// </summary>
    private readonly Random _random;

    /// <summary>
    /// Lock object for thread-safe Random access.
    /// </summary>
    private readonly object _randomLock = new();

    /// <summary>
    /// Shared Random instance for random selections.
    /// WARNING: Not thread-safe for parallel access. Use GetNextRandom() in parallel code.
    /// </summary>
    public Random Random => _random;

    /// <summary>
    /// The seed used for Faker and Random (for debugging/logging).
    /// </summary>
    public int Seed { get; }

    // === Caches populated by seeders ===

    private readonly List<ILanguage> _languages = new();
    private readonly List<IContentType> _elementTypes = new();
    private readonly List<IContentType> _simpleDocTypes = new();
    private readonly List<IContentType> _mediumDocTypes = new();
    private readonly List<IContentType> _complexDocTypes = new();
    private readonly List<IDataType> _blockListDataTypes = new();
    private readonly List<IDataType> _blockGridDataTypes = new();
    private readonly List<IMedia> _mediaItems = new();
    private readonly List<IContent> _createdContent = new();

    /// <summary>
    /// Languages created by LanguageSeeder (used by DictionarySeeder, ContentSeeder).
    /// </summary>
    public IReadOnlyList<ILanguage> Languages => _languages;

    /// <summary>
    /// Element types created by DocumentTypeSeeder (used by ContentSeeder).
    /// </summary>
    public IReadOnlyList<IContentType> ElementTypes => _elementTypes;

    /// <summary>
    /// Simple document types created by DocumentTypeSeeder.
    /// </summary>
    public IReadOnlyList<IContentType> SimpleDocTypes => _simpleDocTypes;

    /// <summary>
    /// Medium document types created by DocumentTypeSeeder.
    /// </summary>
    public IReadOnlyList<IContentType> MediumDocTypes => _mediumDocTypes;

    /// <summary>
    /// Complex document types created by DocumentTypeSeeder.
    /// </summary>
    public IReadOnlyList<IContentType> ComplexDocTypes => _complexDocTypes;

    /// <summary>
    /// Block List data types created by DocumentTypeSeeder.
    /// </summary>
    public IReadOnlyList<IDataType> BlockListDataTypes => _blockListDataTypes;

    /// <summary>
    /// Block Grid data types created by DocumentTypeSeeder.
    /// </summary>
    public IReadOnlyList<IDataType> BlockGridDataTypes => _blockGridDataTypes;

    /// <summary>
    /// Media items created by MediaSeeder (used by ContentSeeder).
    /// </summary>
    public IReadOnlyList<IMedia> MediaItems => _mediaItems;

    /// <summary>
    /// Content items created by ContentSeeder (used for linking).
    /// </summary>
    public IReadOnlyList<IContent> CreatedContent => _createdContent;

    #region Collection Mutation Methods

    /// <summary>Sets the languages collection (replaces existing).</summary>
    public void SetLanguages(IEnumerable<ILanguage> languages)
    {
        _languages.Clear();
        _languages.AddRange(languages);
    }

    /// <summary>Adds an element type to the cache.</summary>
    public void AddElementType(IContentType elementType) => _elementTypes.Add(elementType);

    /// <summary>Adds element types to the cache.</summary>
    public void AddElementTypes(IEnumerable<IContentType> elementTypes) => _elementTypes.AddRange(elementTypes);

    /// <summary>Adds a simple document type to the cache.</summary>
    public void AddSimpleDocType(IContentType docType) => _simpleDocTypes.Add(docType);

    /// <summary>Adds simple document types to the cache.</summary>
    public void AddSimpleDocTypes(IEnumerable<IContentType> docTypes) => _simpleDocTypes.AddRange(docTypes);

    /// <summary>Adds a medium document type to the cache.</summary>
    public void AddMediumDocType(IContentType docType) => _mediumDocTypes.Add(docType);

    /// <summary>Adds medium document types to the cache.</summary>
    public void AddMediumDocTypes(IEnumerable<IContentType> docTypes) => _mediumDocTypes.AddRange(docTypes);

    /// <summary>Adds a complex document type to the cache.</summary>
    public void AddComplexDocType(IContentType docType) => _complexDocTypes.Add(docType);

    /// <summary>Adds complex document types to the cache.</summary>
    public void AddComplexDocTypes(IEnumerable<IContentType> docTypes) => _complexDocTypes.AddRange(docTypes);

    /// <summary>Adds block list data types to the cache.</summary>
    public void AddBlockListDataTypes(IEnumerable<IDataType> dataTypes) => _blockListDataTypes.AddRange(dataTypes);

    /// <summary>Adds block grid data types to the cache.</summary>
    public void AddBlockGridDataTypes(IEnumerable<IDataType> dataTypes) => _blockGridDataTypes.AddRange(dataTypes);

    /// <summary>Adds a media item to the cache.</summary>
    public void AddMediaItem(IMedia media) => _mediaItems.Add(media);

    /// <summary>Adds media items to the cache.</summary>
    public void AddMediaItems(IEnumerable<IMedia> mediaItems) => _mediaItems.AddRange(mediaItems);

    /// <summary>Adds a content item to the cache.</summary>
    public void AddContent(IContent content) => _createdContent.Add(content);

    /// <summary>Adds content items to the cache.</summary>
    public void AddContentItems(IEnumerable<IContent> contentItems) => _createdContent.AddRange(contentItems);

    #endregion

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
    /// Cache of property types by content type ID and alias for quick lookups (N+1 prevention).
    /// Key: (ContentTypeId, PropertyAlias), Value: IPropertyType
    /// </summary>
    public Dictionary<(int ContentTypeId, string Alias), IPropertyType> PropertyTypeCache { get; } = new();

    /// <summary>
    /// Cache of content types by ID for quick lookups.
    /// </summary>
    public Dictionary<int, IContentType> ContentTypeCache { get; } = new();

    /// <summary>
    /// Creates a new SeederExecutionContext with optional seed for reproducibility.
    /// </summary>
    /// <param name="fakerSeed">Optional seed for deterministic data generation. If null, generates a random seed.</param>
    public SeederExecutionContext(int? fakerSeed = null)
    {
        // Use a better seed source than Environment.TickCount (which repeats every ~24 days)
        // Guid.NewGuid() provides better entropy for non-reproducible runs
        Seed = fakerSeed ?? Math.Abs(Guid.NewGuid().GetHashCode());
        Faker = new Faker("en") { Random = new Randomizer(Seed) };
        _random = new Random(Seed);
    }

    /// <summary>
    /// Gets the next random integer in a thread-safe manner.
    /// Use this instead of Random.Next() in parallel code.
    /// </summary>
    public int GetNextRandom()
    {
        lock (_randomLock)
        {
            return _random.Next();
        }
    }

    /// <summary>
    /// Gets the next random integer up to maxValue in a thread-safe manner.
    /// Use this instead of Random.Next(maxValue) in parallel code.
    /// </summary>
    public int GetNextRandom(int maxValue)
    {
        lock (_randomLock)
        {
            return _random.Next(maxValue);
        }
    }

    /// <summary>
    /// Gets a batch of random integers for use in parallel operations.
    /// Pre-generates values to avoid lock contention during parallel execution.
    /// </summary>
    /// <param name="count">Number of random values to generate.</param>
    /// <returns>Array of random integers.</returns>
    public int[] GetRandomBatch(int count)
    {
        lock (_randomLock)
        {
            var batch = new int[count];
            for (int i = 0; i < count; i++)
            {
                batch[i] = _random.Next();
            }
            return batch;
        }
    }
}
