namespace Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// Types of prefixes used for naming seeded data.
/// </summary>
public enum PrefixType
{
    /// <summary>Data type prefix.</summary>
    DataType,
    /// <summary>Element type prefix.</summary>
    ElementType,
    /// <summary>Variant document type prefix.</summary>
    VariantDocType,
    /// <summary>Invariant document type prefix.</summary>
    InvariantDocType,
    /// <summary>Media item prefix.</summary>
    Media,
    /// <summary>Content node prefix.</summary>
    Content,
    /// <summary>User prefix.</summary>
    User,
    /// <summary>Dictionary item prefix.</summary>
    Dictionary
}

/// <summary>
/// Predefined dataset size presets for quick configuration.
/// </summary>
public enum SeederPreset
{
    /// <summary>
    /// Custom configuration - use individual settings from SeederConfiguration.
    /// </summary>
    Custom,

    /// <summary>
    /// Small dataset for quick testing (~200 items total).
    /// Languages: 3, Content: 50, Media: 30, Users: 5
    /// </summary>
    Small,

    /// <summary>
    /// Medium dataset for moderate testing (~2,000 items total).
    /// Languages: 10, Content: 500, Media: 500, Users: 20
    /// </summary>
    Medium,

    /// <summary>
    /// Large dataset for performance testing (~25,000 items total).
    /// Languages: 20, Content: 10,000, Media: 5,000, Users: 50
    /// </summary>
    Large,

    /// <summary>
    /// Massive dataset for stress testing (~100,000 items total).
    /// Languages: 30, Content: 50,000, Media: 20,000, Users: 100
    /// </summary>
    Massive
}

/// <summary>
/// Runtime options for the PerformanceTestDataSeeder package.
/// Configure in appsettings.json under "PerformanceTestDataSeeder:Options" section.
/// </summary>
public class SeederOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PerformanceTestDataSeeder:Options";

    /// <summary>
    /// Predefined dataset size preset. When set to anything other than Custom,
    /// this overrides the SeederConfiguration values.
    /// Default: Custom (use SeederConfiguration values)
    /// </summary>
    public SeederPreset Preset { get; set; } = SeederPreset.Custom;

    /// <summary>
    /// Enable/disable the entire seeder package.
    /// Default: false (must be explicitly enabled)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Stop seeding if any seeder fails.
    /// If false, errors are logged and the next seeder continues.
    /// Default: false
    /// </summary>
    public bool StopOnError { get; set; } = false;

    /// <summary>
    /// Seed for Faker/Random for reproducible test data.
    /// If null, uses current time (non-deterministic).
    /// Set to a fixed value for reproducible test data across runs.
    /// </summary>
    public int? FakerSeed { get; set; }

    /// <summary>
    /// Progress reporting interval as a percentage (e.g., 10 = log every 10%).
    /// Set to 0 to disable progress logging.
    /// Default: 10
    /// </summary>
    public int ProgressIntervalPercent { get; set; } = 10;

    /// <summary>
    /// Individual seeder enable/disable flags.
    /// </summary>
    public SeederEnableFlags EnabledSeeders { get; set; } = new();

    /// <summary>
    /// Configurable prefixes for naming seeded data.
    /// </summary>
    public SeederPrefixes Prefixes { get; set; } = new();

    /// <summary>
    /// Custom cultures to use instead of the default 30.
    /// If null, uses the built-in culture pool.
    /// </summary>
    public string[]? CustomCultures { get; set; }

    /// <summary>
    /// Number of items to process in each database batch.
    /// Higher values improve performance but use more memory.
    /// Default: 50
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Maximum degree of parallelism for CPU-bound operations (e.g., image generation).
    /// Set to 1 to disable parallel processing.
    /// Default: 4
    /// </summary>
    public int ParallelDegree { get; set; } = 4;

    /// <summary>
    /// Automatically publish content after creation.
    /// If true, content will be published for all configured languages (variant) or immediately (invariant).
    /// Default: false
    /// </summary>
    public bool PublishContent { get; set; } = false;

    /// <summary>
    /// Number of content items to publish in each batch when PublishContent is true.
    /// Higher values improve performance but use more memory.
    /// Set to 0 to publish immediately after each save (legacy behavior).
    /// Default: 50
    /// </summary>
    public int PublishBatchSize { get; set; } = 50;

    /// <summary>
    /// Run in dry-run mode. Logs what would be created without persisting to database.
    /// Useful for testing configuration before actual seeding.
    /// Default: false
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Domain suffix for content domains.
    /// Domains are created as "test-{contentId}-{culture}.{DomainSuffix}".
    /// Set to your actual domain for production-like testing.
    /// Default: "localhost"
    /// </summary>
    public string DomainSuffix { get; set; } = "localhost";

    /// <summary>
    /// Skip creating content domains entirely.
    /// Useful when you don't need multi-site routing or want to configure domains manually.
    /// Default: false
    /// </summary>
    public bool SkipContentDomains { get; set; } = false;
}

/// <summary>
/// Enable/disable flags for individual seeders.
/// </summary>
public class SeederEnableFlags
{
    /// <summary>
    /// Enable LanguageSeeder. Default: true
    /// </summary>
    public bool Languages { get; set; } = true;

    /// <summary>
    /// Enable DictionarySeeder. Default: true
    /// </summary>
    public bool Dictionary { get; set; } = true;

    /// <summary>
    /// Enable DataTypeSeeder. Default: true
    /// </summary>
    public bool DataTypes { get; set; } = true;

    /// <summary>
    /// Enable DocumentTypeSeeder. Default: true
    /// </summary>
    public bool DocumentTypes { get; set; } = true;

    /// <summary>
    /// Enable MediaSeeder. Default: true
    /// </summary>
    public bool Media { get; set; } = true;

    /// <summary>
    /// Enable ContentSeeder. Default: true
    /// </summary>
    public bool Content { get; set; } = true;

    /// <summary>
    /// Enable UserSeeder. Default: true
    /// </summary>
    public bool Users { get; set; } = true;
}

/// <summary>
/// Configurable prefixes for naming seeded data.
/// </summary>
public class SeederPrefixes
{
    /// <summary>
    /// Prefix for data type names. Default: "Test_"
    /// </summary>
    public string DataType { get; set; } = "Test_";

    /// <summary>
    /// Prefix for element type aliases. Default: "testElement"
    /// </summary>
    public string ElementType { get; set; } = "testElement";

    /// <summary>
    /// Prefix for variant document type aliases. Default: "testVariant"
    /// </summary>
    public string VariantDocType { get; set; } = "testVariant";

    /// <summary>
    /// Prefix for invariant document type aliases. Default: "testInvariant"
    /// </summary>
    public string InvariantDocType { get; set; } = "testInvariant";

    /// <summary>
    /// Prefix for media item names. Default: "Test_"
    /// </summary>
    public string Media { get; set; } = "Test_";

    /// <summary>
    /// Prefix for content node names. Default: "Test_"
    /// </summary>
    public string Content { get; set; } = "Test_";

    /// <summary>
    /// Prefix for test user usernames. Default: "TestUser_"
    /// </summary>
    public string User { get; set; } = "TestUser_";

    /// <summary>
    /// Prefix for dictionary item keys. Default: "Dict_"
    /// </summary>
    public string Dictionary { get; set; } = "Dict_";
}
