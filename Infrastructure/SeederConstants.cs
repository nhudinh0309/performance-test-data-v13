namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Constants used throughout the seeder package.
/// These values are not user-configurable but document important limits and defaults.
/// </summary>
public static class SeederConstants
{
    /// <summary>
    /// Default maximum number of block elements to add to a Block List or Block Grid.
    /// This value is configurable via DocumentTypes.MaxBlocksPerEditor in SeederConfiguration.
    /// </summary>
    public const int DefaultMaxBlocksPerEditor = 30;

    /// <summary>
    /// Default number of columns in Block Grid layouts.
    /// Standard 12-column grid system.
    /// </summary>
    public const int DefaultGridColumns = 12;

    /// <summary>
    /// Page size for paginated database queries.
    /// Balances memory usage with query efficiency.
    /// </summary>
    public const int PaginationPageSize = 500;

    /// <summary>
    /// Default width for generated test images in pixels.
    /// </summary>
    public const int DefaultImageWidth = 100;

    /// <summary>
    /// Default height for generated test images in pixels.
    /// </summary>
    public const int DefaultImageHeight = 100;

    /// <summary>
    /// Size of placeholder video files in bytes (100KB).
    /// Creates minimal file signatures for testing without large file sizes.
    /// </summary>
    public const int PlaceholderVideoSizeBytes = 1024 * 100;

    /// <summary>
    /// Default column span for grid items when not specified.
    /// Full width in a 12-column grid.
    /// </summary>
    public const int DefaultGridColumnSpan = 12;

    /// <summary>
    /// Default row span for grid items when not specified.
    /// </summary>
    public const int DefaultGridRowSpan = 1;
}
