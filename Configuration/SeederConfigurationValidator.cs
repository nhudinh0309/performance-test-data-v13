namespace Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// Validates SeederConfiguration values to catch configuration errors early.
/// </summary>
public class SeederConfigurationValidator
{
    /// <summary>
    /// Maximum number of languages supported (based on available cultures pool).
    /// </summary>
    public const int MaxLanguages = 30;

    /// <summary>
    /// Maximum nesting depth for nested blocks (to prevent excessive recursion).
    /// </summary>
    public const int MaxNestingDepth = 10;

    /// <summary>
    /// Validates the seeder configuration.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation result with any errors found</returns>
    public ValidationResult Validate(SeederConfiguration config)
    {
        var errors = new List<string>();

        // Validate Languages
        ValidateNonNegative(config.Languages.Count, "Languages.Count", errors);
        if (config.Languages.Count > MaxLanguages)
        {
            errors.Add($"Languages.Count ({config.Languages.Count}) cannot exceed {MaxLanguages} (available cultures)");
        }

        // Validate Dictionary
        ValidateNonNegative(config.Dictionary.RootFolders, "Dictionary.RootFolders", errors);
        ValidateNonNegative(config.Dictionary.SectionsPerRoot, "Dictionary.SectionsPerRoot", errors);
        ValidateNonNegative(config.Dictionary.ItemsPerSection, "Dictionary.ItemsPerSection", errors);

        // Validate Users
        ValidateNonNegative(config.Users.Count, "Users.Count", errors);

        // Validate DataTypes
        ValidateDataTypesConfig(config.DataTypes, errors);

        // Validate DocumentTypes
        ValidateDocumentTypesConfig(config.DocumentTypes, errors);

        // Validate Media
        ValidateMediaConfig(config.Media, errors);

        // Validate Content
        ValidateContentConfig(config.Content, errors);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private void ValidateNonNegative(int value, string name, List<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{name} must be >= 0 (got {value})");
        }
    }

    private void ValidateDataTypesConfig(DataTypesConfig config, List<string> errors)
    {
        ValidateNonNegative(config.ListView, "DataTypes.ListView", errors);
        ValidateNonNegative(config.MultiNodeTreePicker, "DataTypes.MultiNodeTreePicker", errors);
        ValidateNonNegative(config.RichTextEditor, "DataTypes.RichTextEditor", errors);
        ValidateNonNegative(config.MediaPicker, "DataTypes.MediaPicker", errors);
        ValidateNonNegative(config.Textarea, "DataTypes.Textarea", errors);
        ValidateNonNegative(config.Dropdown, "DataTypes.Dropdown", errors);
        ValidateNonNegative(config.Numeric, "DataTypes.Numeric", errors);
    }

    private void ValidateDocumentTypesConfig(DocumentTypesConfig config, List<string> errors)
    {
        ValidateComplexityConfig(config.ElementTypes, "DocumentTypes.ElementTypes", errors);
        ValidateComplexityConfig(config.VariantDocTypes, "DocumentTypes.VariantDocTypes", errors);
        ValidateComplexityConfig(config.InvariantDocTypes, "DocumentTypes.InvariantDocTypes", errors);
        ValidateNonNegative(config.BlockList, "DocumentTypes.BlockList", errors);
        ValidateNonNegative(config.BlockGrid, "DocumentTypes.BlockGrid", errors);

        // Validate NestingDepth
        if (config.NestingDepth < 1)
        {
            errors.Add($"DocumentTypes.NestingDepth must be >= 1 (got {config.NestingDepth})");
        }
        if (config.NestingDepth > MaxNestingDepth)
        {
            errors.Add($"DocumentTypes.NestingDepth cannot exceed {MaxNestingDepth} (got {config.NestingDepth})");
        }
    }

    private void ValidateComplexityConfig(ComplexityConfig config, string prefix, List<string> errors)
    {
        ValidateNonNegative(config.Simple, $"{prefix}.Simple", errors);
        ValidateNonNegative(config.Medium, $"{prefix}.Medium", errors);
        ValidateNonNegative(config.Complex, $"{prefix}.Complex", errors);
    }

    private void ValidateMediaConfig(MediaConfig config, List<string> errors)
    {
        ValidateMediaTypeConfig(config.PDF, "Media.PDF", errors);
        ValidateMediaTypeConfig(config.PNG, "Media.PNG", errors);
        ValidateMediaTypeConfig(config.JPG, "Media.JPG", errors);
        ValidateMediaTypeConfig(config.Video, "Media.Video", errors);
    }

    private void ValidateMediaTypeConfig(MediaTypeConfig config, string prefix, List<string> errors)
    {
        ValidateNonNegative(config.Count, $"{prefix}.Count", errors);
        ValidateNonNegative(config.FolderCount, $"{prefix}.FolderCount", errors);

        if (config.Count > 0 && config.FolderCount <= 0)
        {
            errors.Add($"{prefix}.FolderCount must be > 0 when {prefix}.Count > 0");
        }
    }

    private void ValidateContentConfig(ContentConfig config, List<string> errors)
    {
        ValidateNonNegative(config.TotalTarget, "Content.TotalTarget", errors);
        ValidateNonNegative(config.SimplePercent, "Content.SimplePercent", errors);
        ValidateNonNegative(config.MediumPercent, "Content.MediumPercent", errors);
        ValidateNonNegative(config.ComplexPercent, "Content.ComplexPercent", errors);
        ValidateNonNegative(config.RootSections, "Content.RootSections", errors);
        ValidateNonNegative(config.CategoriesPerSection, "Content.CategoriesPerSection", errors);
        ValidateNonNegative(config.PagesPerCategory, "Content.PagesPerCategory", errors);

        // Validate percentages sum to 100
        var percentSum = config.SimplePercent + config.MediumPercent + config.ComplexPercent;
        if (percentSum != 100)
        {
            errors.Add($"Content percentages must sum to 100 (SimplePercent + MediumPercent + ComplexPercent = {percentSum})");
        }

        // Validate individual percentages are valid
        if (config.SimplePercent > 100)
        {
            errors.Add($"Content.SimplePercent cannot exceed 100 (got {config.SimplePercent})");
        }
        if (config.MediumPercent > 100)
        {
            errors.Add($"Content.MediumPercent cannot exceed 100 (got {config.MediumPercent})");
        }
        if (config.ComplexPercent > 100)
        {
            errors.Add($"Content.ComplexPercent cannot exceed 100 (got {config.ComplexPercent})");
        }
    }
}

/// <summary>
/// Result of configuration validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors (empty if valid).
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
