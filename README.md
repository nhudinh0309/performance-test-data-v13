# Umbraco 13 Performance Test Data Seeder

This project is an **Umbraco CMS v13.10.0 site** with custom seeders for generating large amounts of test data.
It is designed to simulate **enterprise-scale datasets** for **performance and load testing** of the Umbraco backoffice.

---

## Features

- **Languages**: Seeds configurable number of languages (default: 20) from a pool of 30 cultures.
- **Dictionary Items**: Seeds dictionary items organized into 3 folder levels, each with translations for all languages.
- **Users**: Seeds test users distributed across 5 user groups (Admin, Editor, Writer, Sensitive Data, Translator).
- **Data Types**: Seeds various data types (ListView, MultiNodeTreePicker, RichTextEditor, MediaPicker, Textarea, Dropdown, Numeric).
- **Document Types**: Seeds Element Types (for Block List/Grid) and Document Types (Variant + Invariant) with Templates.
- **Media**: Seeds media items (PDF, PNG, JPG, Videos) organized in folders.
- **Content**: Seeds content nodes with multi-level hierarchy and variant content support.

---

## Target Data Quantities (Configurable via SeederConfig.json)

| Category | Default Target |
|----------|---------------|
| Languages | 20 |
| Dictionary Items | 1,500 |
| Users | 30 |
| Data Types | 250 |
| Document Types | 280 (Element + Variant + Invariant) |
| Media | 23,000+ |
| Content | 25,000+ |

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Umbraco CMS 13.10.0](https://our.umbraco.com/download)
- SQLite or SQL Server database
- [Bogus](https://github.com/bchavez/Bogus) NuGet package for generating fake data
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) for generating test images

---

## Project Structure

```
DummyDataSeeder/
├── README.md                 → Module documentation
├── SeederConfig.json         → Configuration file (copy to project root)
├── SeederConfiguration.cs    → Configuration model
├── Seeders/                  → Seeder classes
│   ├── LanguageSeeder.cs
│   ├── DictionarySeeder.cs
│   ├── UserSeeder.cs
│   ├── DataTypeSeeder.cs
│   ├── DocumentTypeSeeder.cs
│   ├── MediaSeeder.cs
│   └── ContentSeeder.cs
└── docs/                     → Detailed documentation
    ├── SEEDERS.md
    ├── CONFIGURATION.md
    └── TEMPLATES.md

Views/                        → Generated templates for Document Types
SeederConfig.json             → Configuration file for seeder targets
Program.cs                    → Registers seeders as hosted services
appsettings.json              → Database and Umbraco configuration
```

> **Note:** The `DummyDataSeeder` module is designed to be **reusable** - see [DummyDataSeeder/README.md](DummyDataSeeder/README.md) for installation instructions in other projects.

---

## Configuration (SeederConfig.json)

All seeder targets are configurable via `SeederConfig.json`:

```json
{
  "SeederConfiguration": {
    "Languages": { "Count": 20 },
    "Dictionary": { "TotalItems": 1500, "FolderCount": 3, "ItemsPerFolder": 50 },
    "Users": { "Count": 30 },
    "DataTypes": {
      "ListView": 50,
      "MultiNodeTreePicker": 50,
      "RichTextEditor": 30,
      "MediaPicker": 30,
      "Textarea": 30,
      "Dropdown": 30,
      "Numeric": 30
    },
    "DocumentTypes": {
      "ElementTypes": { "Simple": 100, "Medium": 50, "Complex": 30 },
      "VariantDocTypes": { "Simple": 55, "Medium": 33, "Complex": 22 },
      "InvariantDocTypes": { "Simple": 20, "Medium": 12, "Complex": 8 },
      "BlockList": 40,
      "BlockGrid": 60
    },
    "Media": {
      "PDF": { "Count": 200, "FolderCount": 10 },
      "PNG": { "Count": 200, "FolderCount": 10 },
      "JPG": { "Count": 200, "FolderCount": 10 },
      "Video": { "Count": 10 }
    },
    "Content": {
      "TotalTarget": 300,
      "SimplePercent": 50,
      "MediumPercent": 30,
      "ComplexPercent": 20,
      "RootSections": 50,
      "CategoriesPerSection": 10,
      "PagesPerCategory": 100
    }
  }
}
```

---

## Document Type Complexity

### Element Types (for Block List/Grid)
- **Simple**: 3 properties (title, description, isActive), 1 tab
- **Medium**: 5 properties (title, subtitle, description, isVisible, linkedContent), 2 tabs
- **Complex**: 10 properties (title, summary, mainImage, thumbnailImage, primaryLink, secondaryLink, isEnabled, cssClass, metaTitle, metaDescription), 5 tabs

### Document Types
- **Simple**: 3 properties (title, description, isPublished)
- **Medium**: 5 properties (title, subtitle, summary, relatedContent, blocks)
- **Complex**: 10 properties (title, subtitle, bodyText, mainImage, thumbnailImage, primaryContent, secondaryContent, tertiaryContent, headerBlocks, footerBlocks, mainGrid)

### Block Content Distribution
- **Medium Content**: 1 simple block item
- **Complex Content**: 1 simple + 1 medium + 1 complex block item (for both Block List and Block Grid)

---

## Variant Content Support

For **Variant Document Types** (with `ContentVariation.Culture`):
- Content names are set for **all languages**: `{name} ({culture})`
- Property values are set for **each culture** separately
- **Domains (hostname + culture)** are automatically assigned to root content nodes
  - Format: `test-{contentId}-{culture}.localhost`
  - Example: `test-1234-en-us.localhost`, `test-1234-fr-fr.localhost`

---

## Templates

Templates are automatically generated for each Document Type with full property rendering:

- **Simple Template**: Renders title, description, isPublished
- **Medium Template**: Renders title, subtitle, summary, relatedContent, Block List
- **Complex Template**: Renders all properties including:
  - Text content (title, subtitle, bodyText)
  - Media (mainImage, thumbnailImage with `<img>` tags)
  - Related content links (primaryContent, secondaryContent, tertiaryContent)
  - Block List (headerBlocks, footerBlocks) with dynamic property rendering
  - Block Grid (mainGrid) with column/row span info and nested areas

---

## Seeder Execution Order

Seeders run in registration order (configured in `Program.cs`):

1. **LanguageSeeder** - Languages (required for dictionaries and variant content)
2. **DictionarySeeder** - Dictionary items (depends on Languages)
3. **DataTypeSeeder** - Data Types (required for Document Types)
4. **DocumentTypeSeeder** - Element Types, Document Types, Templates (depends on Data Types)
5. **MediaSeeder** - Media items (required for content media pickers)
6. **UserSeeder** - Test users
7. **ContentSeeder** - Content nodes (depends on Document Types and Media)

All seeders check `IRuntimeState.Level == RuntimeLevel.Run` before executing to ensure Umbraco is fully installed.

---

## How to Run

1. Clone the repository
2. Configure `SeederConfig.json` with desired targets
3. Run the application:
   ```bash
   dotnet run
   ```
4. Complete Umbraco installation wizard
5. Seeders will automatically run after Umbraco is installed
6. Access backoffice at `/umbraco` to view seeded data

---

## Notes

- Seeders are **idempotent** - they check for existing data before seeding
- Content is saved as **draft** (not published) - publish manually or via script as needed
- Large data sets may take several minutes to seed
- Monitor console output for progress updates
