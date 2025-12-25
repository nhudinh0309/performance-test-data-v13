/// <summary>
/// Configuration model for SeederConfig.json
/// All default values are set to small numbers for development/testing.
/// Override in SeederConfig.json for larger datasets.
/// </summary>
public class SeederConfiguration
{
    public LanguagesConfig Languages { get; set; } = new();
    public DictionaryConfig Dictionary { get; set; } = new();
    public UsersConfig Users { get; set; } = new();
    public DataTypesConfig DataTypes { get; set; } = new();
    public DocumentTypesConfig DocumentTypes { get; set; } = new();
    public MediaConfig Media { get; set; } = new();
    public ContentConfig Content { get; set; } = new();
}

public class LanguagesConfig
{
    public int Count { get; set; } = 20;
}

public class DictionaryConfig
{
    public int RootFolders { get; set; } = 10;
    public int SectionsPerRoot { get; set; } = 5;
    public int ItemsPerSection { get; set; } = 30;

    public int TotalItems => RootFolders * SectionsPerRoot * ItemsPerSection;
}

public class UsersConfig
{
    public int Count { get; set; } = 30;
}

public class DataTypesConfig
{
    public int ListView { get; set; } = 30;
    public int MultiNodeTreePicker { get; set; } = 40;
    public int RichTextEditor { get; set; } = 10;
    public int MediaPicker { get; set; } = 10;
    public int Textarea { get; set; } = 10;
    public int Dropdown { get; set; } = 10;
    public int Numeric { get; set; } = 10;

    public int Total => ListView + MultiNodeTreePicker + RichTextEditor + MediaPicker + Textarea + Dropdown + Numeric;
}

public class DocumentTypesConfig
{
    public ComplexityConfig ElementTypes { get; set; } = new() { Simple = 65, Medium = 39, Complex = 26 };
    public ComplexityConfig VariantDocTypes { get; set; } = new() { Simple = 55, Medium = 33, Complex = 22 };
    public ComplexityConfig InvariantDocTypes { get; set; } = new() { Simple = 20, Medium = 12, Complex = 8 };
    public int BlockList { get; set; } = 40;
    public int BlockGrid { get; set; } = 60;

    public int TotalElementTypes => ElementTypes.Total;
    public int TotalDocTypes => VariantDocTypes.Total + InvariantDocTypes.Total;
    public int TotalTemplates => TotalDocTypes;
}

public class ComplexityConfig
{
    public int Simple { get; set; }
    public int Medium { get; set; }
    public int Complex { get; set; }

    public int Total => Simple + Medium + Complex;
}

public class MediaConfig
{
    public MediaTypeConfig PDF { get; set; } = new() { Count = 200, FolderCount = 10 };
    public MediaTypeConfig PNG { get; set; } = new() { Count = 200, FolderCount = 10 };
    public MediaTypeConfig JPG { get; set; } = new() { Count = 200, FolderCount = 10 };
    public MediaTypeConfig Video { get; set; } = new() { Count = 10, FolderCount = 1 };

    public int TotalCount => PDF.Count + PNG.Count + JPG.Count + Video.Count;
}

public class MediaTypeConfig
{
    public int Count { get; set; }
    public int FolderCount { get; set; } = 1;
}

public class ContentConfig
{
    public int TotalTarget { get; set; } = 300;
    public int SimplePercent { get; set; } = 50;
    public int MediumPercent { get; set; } = 30;
    public int ComplexPercent { get; set; } = 20;
    public int RootSections { get; set; } = 50;
    public int CategoriesPerSection { get; set; } = 10;
    public int PagesPerCategory { get; set; } = 100;

    public int SimpleTarget => TotalTarget * SimplePercent / 100;
    public int MediumTarget => TotalTarget * MediumPercent / 100;
    public int ComplexTarget => TotalTarget * ComplexPercent / 100;
}
