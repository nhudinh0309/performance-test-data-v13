# DummyDataSeeder for Umbraco 13

A reusable test data seeder module for Umbraco CMS v13.x. Generates large amounts of test data for performance and load testing.

---

## Folder Structure

```
DummyDataSeeder/
├── README.md                    # This file
├── SeederConfig.json            # Configuration file (copy to project root)
├── SeederConfiguration.cs       # Configuration model
├── Seeders/                     # Seeder classes
│   ├── LanguageSeeder.cs
│   ├── DictionarySeeder.cs
│   ├── UserSeeder.cs
│   ├── DataTypeSeeder.cs
│   ├── DocumentTypeSeeder.cs
│   ├── MediaSeeder.cs
│   └── ContentSeeder.cs
└── docs/                        # Documentation
    ├── SEEDERS.md
    ├── CONFIGURATION.md
    └── TEMPLATES.md
```

---

## Features

| Seeder | Description |
|--------|-------------|
| LanguageSeeder | Seeds languages from a pool of 30 cultures |
| DictionarySeeder | Seeds dictionary items with translations for all languages |
| UserSeeder | Seeds test users across 5 user groups |
| DataTypeSeeder | Seeds various data types (ListView, MNTP, RTE, MediaPicker, etc.) |
| DocumentTypeSeeder | Seeds Element Types, Document Types (Variant + Invariant), and Templates |
| MediaSeeder | Seeds media items (PDF, PNG, JPG, Videos) in folders |
| ContentSeeder | Seeds content with multi-level hierarchy and variant support |

---

## Installation

### 1. Copy Files

Copy the entire `DummyDataSeeder` folder to your Umbraco 13.x project.

### 2. Add NuGet Packages

```bash
dotnet add package Bogus
dotnet add package SixLabors.ImageSharp
```

### 3. Add Configuration File

Copy `SeederConfig.json` from this folder to your project root, or create a new one:

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

### 4. Register in Program.cs

```csharp
// Load SeederConfig.json
builder.Configuration.AddJsonFile("SeederConfig.json", optional: true, reloadOnChange: true);

// Add configuration
builder.Services.Configure<SeederConfiguration>(
    builder.Configuration.GetSection("SeederConfiguration"));

// Register seeders (ORDER MATTERS!)
builder.Services.AddHostedService<LanguageSeeder>();
builder.Services.AddHostedService<DictionarySeeder>();
builder.Services.AddHostedService<DataTypeSeeder>();
builder.Services.AddHostedService<DocumentTypeSeeder>();
builder.Services.AddHostedService<MediaSeeder>();
builder.Services.AddHostedService<UserSeeder>();
builder.Services.AddHostedService<ContentSeeder>();
```

> **Note:** All classes are in the global namespace, so no `using` statement is required.

---

## Seeder Execution Order

Seeders run in registration order:

1. **LanguageSeeder** - Required for dictionaries and variant content
2. **DictionarySeeder** - Depends on Languages
3. **DataTypeSeeder** - Required for Document Types
4. **DocumentTypeSeeder** - Depends on Data Types, creates Element Types first
5. **MediaSeeder** - Required for content media pickers
6. **UserSeeder** - Independent
7. **ContentSeeder** - Depends on Document Types and Media

---

## Document Type Complexity

### Element Types (for Block List/Grid)
| Level | Properties | Tabs |
|-------|-----------|------|
| Simple | 3 (title, description, isActive) | 1 |
| Medium | 5 (title, subtitle, description, isVisible, linkedContent) | 2 |
| Complex | 10 (title, summary, mainImage, thumbnailImage, primaryLink, secondaryLink, isEnabled, cssClass, metaTitle, metaDescription) | 5 |

### Document Types
| Level | Properties |
|-------|-----------|
| Simple | 3 (title, description, isPublished) |
| Medium | 5 (title, subtitle, summary, relatedContent, blocks) |
| Complex | 11 (title, subtitle, bodyText, mainImage, thumbnailImage, primaryContent, secondaryContent, tertiaryContent, headerBlocks, footerBlocks, mainGrid) |

---

## Variant Content Support

For variant Document Types (`ContentVariation.Culture`):
- Content names are set for **all languages**: `{name} ({culture})`
- Property values are set for **each culture** separately
- **Domains** are automatically assigned to root content nodes
  - Format: `test-{contentId}-{culture}.localhost`

---

## Templates

Templates are auto-generated to `Views/` folder with property rendering:

- **Simple**: Renders title, description, isPublished
- **Medium**: Renders text properties, relatedContent, Block List
- **Complex**: Renders all properties including media, content pickers, Block List, and Block Grid

---

## Configuration Presets

### Small (Development)
```json
{
  "Languages": { "Count": 5 },
  "Dictionary": { "TotalItems": 100 },
  "Users": { "Count": 10 },
  "Content": { "TotalTarget": 100 }
}
```

### Medium (Testing)
```json
{
  "Languages": { "Count": 10 },
  "Dictionary": { "TotalItems": 500 },
  "Users": { "Count": 20 },
  "Content": { "TotalTarget": 1000 }
}
```

### Large (Performance Testing)
```json
{
  "Languages": { "Count": 20 },
  "Dictionary": { "TotalItems": 1500 },
  "Users": { "Count": 30 },
  "Content": { "TotalTarget": 25000 }
}
```

---

## Notes

- Seeders are **idempotent** - they check for existing data before seeding
- Content is saved as **draft** (not published)
- All seeders check `IRuntimeState.Level == RuntimeLevel.Run` before executing
- Large data sets may take several minutes to seed

---

## Documentation

See the `docs/` folder for detailed documentation:
- [SEEDERS.md](docs/SEEDERS.md) - Detailed seeder documentation
- [CONFIGURATION.md](docs/CONFIGURATION.md) - Configuration reference
- [TEMPLATES.md](docs/TEMPLATES.md) - Template rendering patterns

---

## Requirements

- Umbraco CMS 13.x
- .NET 8
- Bogus (NuGet)
- SixLabors.ImageSharp (NuGet)
