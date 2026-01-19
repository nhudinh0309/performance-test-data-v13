namespace Umbraco.Community.DummyDataSeeder.Configuration;

/// <summary>
/// Runtime options for the DummyDataSeeder package.
/// Configure in appsettings.json under "DummyDataSeeder:Options" section.
/// </summary>
public class SeederOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "DummyDataSeeder:Options";

    /// <summary>
    /// Enable/disable the entire seeder package.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

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
