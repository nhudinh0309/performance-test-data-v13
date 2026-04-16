namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using Umbraco.Cms.Core;

/// <summary>
/// Constants used throughout the seeder package.
/// These values are not user-configurable but document important limits and defaults.
/// </summary>
public static class SeederConstants
{
    /// <summary>
    /// Maps backend property editor aliases to their v14+ backoffice UI aliases.
    /// Required when creating DataTypes programmatically — the EditorUiAlias must be set
    /// for the new backoffice to render the property editor correctly.
    /// </summary>
    public static readonly Dictionary<string, string> EditorUiAliasMap = new()
    {
        [Constants.PropertyEditors.Aliases.ListView] = "Umb.PropertyEditorUi.Collection",
        [Constants.PropertyEditors.Aliases.MultiNodeTreePicker] = "Umb.PropertyEditorUi.ContentPicker",
        [Constants.PropertyEditors.Aliases.RichText] = "Umb.PropertyEditorUi.Tiptap",
        [Constants.PropertyEditors.Aliases.MediaPicker3] = "Umb.PropertyEditorUi.MediaPicker",
        [Constants.PropertyEditors.Aliases.TextArea] = "Umb.PropertyEditorUi.TextArea",
        [Constants.PropertyEditors.Aliases.DropDownListFlexible] = "Umb.PropertyEditorUi.Dropdown",
        [Constants.PropertyEditors.Aliases.Integer] = "Umb.PropertyEditorUi.Integer",
        [Constants.PropertyEditors.Aliases.BlockList] = "Umb.PropertyEditorUi.BlockList",
        [Constants.PropertyEditors.Aliases.BlockGrid] = "Umb.PropertyEditorUi.BlockGrid",
    };

    /// <summary>
    /// Gets the EditorUiAlias for a given backend property editor alias.
    /// Returns null if no mapping exists.
    /// </summary>
    public static string? GetEditorUiAlias(string editorAlias)
        => EditorUiAliasMap.TryGetValue(editorAlias, out var uiAlias) ? uiAlias : null;

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
