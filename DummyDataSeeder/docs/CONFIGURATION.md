# Configuration Guide

This document explains how to configure the Umbraco Performance Test Data Seeder.

---

## Configuration File

All seeder settings are configured in `SeederConfig.json` at the project root.

### Full Configuration Schema

```json
{
  "SeederConfiguration": {
    "Languages": {
      "Count": 20
    },
    "Dictionary": {
      "TotalItems": 1500,
      "FolderCount": 3,
      "ItemsPerFolder": 50
    },
    "Users": {
      "Count": 30
    },
    "DataTypes": {
      "Total": 250,
      "ListView": 50,
      "MultiNodeTreePicker": 50,
      "RichTextEditor": 30,
      "MediaPicker": 30,
      "Textarea": 30,
      "Dropdown": 30,
      "Numeric": 30
    },
    "DocumentTypes": {
      "TotalDocTypes": 150,
      "ElementTypes": {
        "Total": 180,
        "Simple": 100,
        "Medium": 50,
        "Complex": 30
      },
      "VariantDocTypes": {
        "Total": 110,
        "Simple": 55,
        "Medium": 33,
        "Complex": 22
      },
      "InvariantDocTypes": {
        "Total": 40,
        "Simple": 20,
        "Medium": 12,
        "Complex": 8
      },
      "BlockList": 40,
      "BlockGrid": 60
    },
    "Media": {
      "TotalCount": 610,
      "PDF": {
        "Count": 200,
        "FolderCount": 10
      },
      "PNG": {
        "Count": 200,
        "FolderCount": 10
      },
      "JPG": {
        "Count": 200,
        "FolderCount": 10
      },
      "Video": {
        "Count": 10
      }
    },
    "Content": {
      "TotalTarget": 25000,
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

## Configuration Sections

### Languages

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Count` | int | 20 | Number of languages to create (max 30) |

**Note:** Languages are selected from a predefined pool of 30 cultures. The first N cultures are used.

---

### Dictionary

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TotalItems` | int | 1500 | Total dictionary items to create |
| `FolderCount` | int | 3 | Number of root folders |
| `ItemsPerFolder` | int | 50 | Items per folder level |

**Formula:** `TotalItems ≈ FolderCount × ItemsPerFolder × 3 (levels)`

---

### Users

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Count` | int | 30 | Number of test users to create |

**Note:** Users are distributed evenly across 5 groups (Admin, Editor, Writer, Sensitive Data, Translator).

---

### DataTypes

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Total` | int | 250 | Total data types (calculated) |
| `ListView` | int | 50 | List View data types |
| `MultiNodeTreePicker` | int | 50 | MNTP data types |
| `RichTextEditor` | int | 30 | Rich Text Editor data types |
| `MediaPicker` | int | 30 | Media Picker 3 data types |
| `Textarea` | int | 30 | Textarea data types |
| `Dropdown` | int | 30 | Dropdown data types |
| `Numeric` | int | 30 | Numeric/Integer data types |

---

### DocumentTypes

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TotalDocTypes` | int | 150 | Total document types |
| `BlockList` | int | 40 | Block List data types to create |
| `BlockGrid` | int | 60 | Block Grid data types to create |

#### ElementTypes (for Block List/Grid)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Total` | int | 180 | Total element types |
| `Simple` | int | 100 | Simple element types (3 props, 1 tab) |
| `Medium` | int | 50 | Medium element types (5 props, 2 tabs) |
| `Complex` | int | 30 | Complex element types (10 props, 5 tabs) |

#### VariantDocTypes (with culture variation)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Total` | int | 110 | Total variant document types |
| `Simple` | int | 55 | Simple variant doc types |
| `Medium` | int | 33 | Medium variant doc types |
| `Complex` | int | 22 | Complex variant doc types |

#### InvariantDocTypes (no culture variation)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Total` | int | 40 | Total invariant document types |
| `Simple` | int | 20 | Simple invariant doc types |
| `Medium` | int | 12 | Medium invariant doc types |
| `Complex` | int | 8 | Complex invariant doc types |

---

### Media

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TotalCount` | int | 610 | Total media items (calculated) |

#### PDF

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Count` | int | 200 | Number of PDF files |
| `FolderCount` | int | 10 | Number of PDF subfolders |

#### PNG

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Count` | int | 200 | Number of PNG images |
| `FolderCount` | int | 10 | Number of PNG subfolders |

#### JPG

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Count` | int | 200 | Number of JPG images |
| `FolderCount` | int | 10 | Number of JPG subfolders |

#### Video

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Count` | int | 10 | Number of video files |

---

### Content

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TotalTarget` | int | 25000 | Target total content nodes |
| `SimplePercent` | int | 50 | Percentage of simple content |
| `MediumPercent` | int | 30 | Percentage of medium content |
| `ComplexPercent` | int | 20 | Percentage of complex content |
| `RootSections` | int | 50 | Number of root sections |
| `CategoriesPerSection` | int | 10 | Categories per section |
| `PagesPerCategory` | int | 100 | Pages per category |

**Note:** Percentages should sum to 100.

---

## Configuration Class

The configuration is mapped to `SeederConfiguration.cs`:

```csharp
namespace TestData13._10._0.DummyDataSeeder;

public class SeederConfiguration
{
    public LanguageConfig Languages { get; set; } = new();
    public DictionaryConfig Dictionary { get; set; } = new();
    public UserConfig Users { get; set; } = new();
    public DataTypeConfig DataTypes { get; set; } = new();
    public DocumentTypeConfig DocumentTypes { get; set; } = new();
    public MediaConfig Media { get; set; } = new();
    public ContentConfig Content { get; set; } = new();
}

// Sub-configuration classes...
```

---

## Loading Configuration

Configuration is loaded in `Program.cs`:

```csharp
// Load seeder configuration from JSON file
builder.Configuration.AddJsonFile("SeederConfig.json", optional: false, reloadOnChange: true);
builder.Services.Configure<SeederConfiguration>(builder.Configuration.GetSection("SeederConfiguration"));
```

Seeders receive configuration via dependency injection:

```csharp
public class MySeeder : IHostedService
{
    private readonly SeederConfiguration _config;

    public MySeeder(IOptions<SeederConfiguration> config)
    {
        _config = config.Value;
    }
}
```

---

## Quick Configuration Presets

### Small Test (Quick Testing)
```json
{
  "SeederConfiguration": {
    "Languages": { "Count": 3 },
    "Dictionary": { "TotalItems": 50, "FolderCount": 2, "ItemsPerFolder": 10 },
    "Users": { "Count": 5 },
    "DocumentTypes": {
      "ElementTypes": { "Simple": 5, "Medium": 3, "Complex": 2 },
      "VariantDocTypes": { "Simple": 3, "Medium": 2, "Complex": 1 },
      "InvariantDocTypes": { "Simple": 2, "Medium": 1, "Complex": 1 },
      "BlockList": 5,
      "BlockGrid": 5
    },
    "Media": {
      "PDF": { "Count": 10, "FolderCount": 2 },
      "PNG": { "Count": 10, "FolderCount": 2 },
      "JPG": { "Count": 10, "FolderCount": 2 },
      "Video": { "Count": 2 }
    },
    "Content": {
      "TotalTarget": 50,
      "SimplePercent": 50,
      "MediumPercent": 30,
      "ComplexPercent": 20,
      "RootSections": 5,
      "CategoriesPerSection": 2,
      "PagesPerCategory": 5
    }
  }
}
```

### Medium Test (Development)
```json
{
  "SeederConfiguration": {
    "Languages": { "Count": 10 },
    "Dictionary": { "TotalItems": 500, "FolderCount": 3, "ItemsPerFolder": 30 },
    "Users": { "Count": 20 },
    "DocumentTypes": {
      "ElementTypes": { "Simple": 30, "Medium": 15, "Complex": 10 },
      "VariantDocTypes": { "Simple": 20, "Medium": 10, "Complex": 5 },
      "InvariantDocTypes": { "Simple": 10, "Medium": 5, "Complex": 3 },
      "BlockList": 20,
      "BlockGrid": 30
    },
    "Media": {
      "PDF": { "Count": 100, "FolderCount": 5 },
      "PNG": { "Count": 100, "FolderCount": 5 },
      "JPG": { "Count": 100, "FolderCount": 5 },
      "Video": { "Count": 5 }
    },
    "Content": {
      "TotalTarget": 1000,
      "SimplePercent": 50,
      "MediumPercent": 30,
      "ComplexPercent": 20,
      "RootSections": 20,
      "CategoriesPerSection": 5,
      "PagesPerCategory": 10
    }
  }
}
```

### Large Test (Performance Testing)
Use the default configuration in README.md for enterprise-scale testing.

---

## Tips

1. **Start Small:** Begin with a small configuration to verify everything works.

2. **Scaling:** Increase values gradually to find performance limits.

3. **Database:** Use SQL Server for large datasets (SQLite may be slow).

4. **Timing:** Large configurations can take 10-30+ minutes to seed.

5. **Memory:** Monitor memory usage for very large media seeding.

6. **Idempotency:** Seeders check for existing data, so running multiple times is safe.
