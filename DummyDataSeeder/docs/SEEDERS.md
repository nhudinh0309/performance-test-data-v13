# Seeders Documentation

This document provides detailed documentation for each seeder in the Umbraco Performance Test Data project.

---

## Table of Contents

1. [LanguageSeeder](#1-languageseeder)
2. [DictionarySeeder](#2-dictionaryseeder)
3. [UserSeeder](#3-userseeder)
4. [DataTypeSeeder](#4-datatypeseeder)
5. [DocumentTypeSeeder](#5-documenttypeseeder)
6. [MediaSeeder](#6-mediaseeder)
7. [ContentSeeder](#7-contentseeder)

---

## 1. LanguageSeeder

**File:** `DummyDataSeeder/LanguageSeeder.cs`

### Purpose
Seeds multiple languages into Umbraco to support multilingual content testing.

### Configuration
```json
{
  "Languages": {
    "Count": 20
  }
}
```

### Available Cultures Pool (30 cultures)
```
en-US, fr-FR, de-DE, es-ES, it-IT,
pt-BR, ru-RU, ja-JP, zh-CN, ko-KR,
nl-NL, sv-SE, pl-PL, tr-TR, ar-SA,
hi-IN, vi-VN, id-ID, th-TH, uk-UA,
cs-CZ, da-DK, fi-FI, el-GR, hu-HU,
no-NO, ro-RO, sk-SK, sl-SI, bg-BG
```

### Behavior
- Creates languages from the first N cultures in the pool (where N = configured count)
- First language created becomes the default language
- Skips execution if Umbraco is not fully installed (`RuntimeLevel != Run`)
- Idempotent: checks existing languages before creating new ones

### Dependencies
- None (runs first in the seeder chain)

### Services Used
- `ILocalizationService` - For creating and saving languages

---

## 2. DictionarySeeder

**File:** `DummyDataSeeder/DictionarySeeder.cs`

### Purpose
Seeds dictionary items with translations for all installed languages, organized in a hierarchical folder structure.

### Configuration
```json
{
  "Dictionary": {
    "TotalItems": 1500,
    "FolderCount": 3,
    "ItemsPerFolder": 50
  }
}
```

### Structure
```
TestDict_Folder_1/
├── TestDict_1_1
├── TestDict_1_2
├── ...
├── TestDict_Folder_1_1/
│   ├── TestDict_1_1_1
│   ├── TestDict_1_1_2
│   └── ...
└── TestDict_Folder_1_2/
    └── ...
TestDict_Folder_2/
└── ...
```

### Behavior
- Creates 3-level folder hierarchy
- Each dictionary item has translations for ALL installed languages
- Translations use Bogus to generate random Lorem Ipsum text
- Translation format: `{culture}: {random_words}`
- Skips if dictionary items with "TestDict_" prefix already exist

### Dependencies
- **LanguageSeeder** - Requires languages to be seeded first

### Services Used
- `ILocalizationService` - For creating dictionary items and translations

---

## 3. UserSeeder

**File:** `DummyDataSeeder/UserSeeder.cs`

### Purpose
Seeds test users distributed across different user groups for permission testing.

### Configuration
```json
{
  "Users": {
    "Count": 30
  }
}
```

### User Distribution (20% each group)
| Group | Users (for 30 total) |
|-------|---------------------|
| Admin | 6 |
| Editor | 6 |
| Writer | 6 |
| Sensitive Data | 6 |
| Translator | 6 |

### User Properties
- **Username:** `TestUser_{index}`
- **Email:** `testuser{index}@example.com`
- **Name:** Random first + last name (via Bogus)

### Behavior
- Distributes users evenly across 5 user groups
- Skips if users with "TestUser_" prefix already exist
- Users are created with identity (no password set)

### Dependencies
- None (but runs after DictionarySeeder in the chain)

### Services Used
- `IUserService` - For creating and managing users

---

## 4. DataTypeSeeder

**File:** `DummyDataSeeder/DataTypeSeeder.cs`

### Purpose
Seeds custom data types that will be used by Document Types.

### Configuration
```json
{
  "DataTypes": {
    "ListView": 50,
    "MultiNodeTreePicker": 50,
    "RichTextEditor": 30,
    "MediaPicker": 30,
    "Textarea": 30,
    "Dropdown": 30,
    "Numeric": 30
  }
}
```

### Data Types Created

| Type | Naming Pattern | Configuration |
|------|---------------|---------------|
| ListView | `Test_ListView_{n}` | Default |
| MultiNodeTreePicker | `Test_MNTP_{n}` | Content picker, 1-5 max items |
| RichTextEditor | `Test_RTE_{n}` | TinyMCE editor |
| MediaPicker | `Test_MediaPicker_{n}` | Media Picker 3 |
| Textarea | `Test_Textarea_{n}` | Multi-line text |
| Dropdown | `Test_Dropdown_{n}` | 5 options, alternating single/multiple |
| Numeric | `Test_Numeric_{n}` | Integer input |

### Behavior
- Skips if data types with "Test_" prefix already exist
- Block List and Block Grid data types are created in DocumentTypeSeeder (requires Element Types first)

### Dependencies
- None (but runs after UserSeeder in the chain)

### Services Used
- `IDataTypeService` - For creating data types
- `PropertyEditorCollection` - For getting property editors

---

## 5. DocumentTypeSeeder

**File:** `DummyDataSeeder/DocumentTypeSeeder.cs`

### Purpose
Seeds Element Types (for blocks), Document Types (variant + invariant), Block List/Grid data types, and Templates.

### Configuration
```json
{
  "DocumentTypes": {
    "ElementTypes": {
      "Simple": 100,
      "Medium": 50,
      "Complex": 30
    },
    "VariantDocTypes": {
      "Simple": 55,
      "Medium": 33,
      "Complex": 22
    },
    "InvariantDocTypes": {
      "Simple": 20,
      "Medium": 12,
      "Complex": 8
    },
    "BlockList": 40,
    "BlockGrid": 60
  }
}
```

### Element Types (for Block List/Grid)

#### Simple Element Type
- **Alias:** `testElementSimple{n}`
- **Properties (3):** title (textstring), description (textarea), isActive (true/false)
- **Tabs (1):** Content

#### Medium Element Type
- **Alias:** `testElementMedium{n}`
- **Properties (5):** title, subtitle, description, isVisible, linkedContent
- **Tabs (2):** Content, Settings

#### Complex Element Type
- **Alias:** `testElementComplex{n}`
- **Properties (10):** title, summary, mainImage, thumbnailImage, primaryLink, secondaryLink, isEnabled, cssClass, metaTitle, metaDescription
- **Tabs (5):** Content, Media, Links, Settings, SEO

### Document Types

#### Simple Document Type
- **Variant Alias:** `testVariantSimple{n}`
- **Invariant Alias:** `testInvariantSimple{n}`
- **Properties (3):** title (textstring), description (textarea), isPublished (true/false)

#### Medium Document Type
- **Variant Alias:** `testVariantMedium{n}`
- **Invariant Alias:** `testInvariantMedium{n}`
- **Properties (5):** title, subtitle, summary, relatedContent (content picker), blocks (Block List)

#### Complex Document Type
- **Variant Alias:** `testVariantComplex{n}`
- **Invariant Alias:** `testInvariantComplex{n}`
- **Properties (10):** title, subtitle, bodyText, mainImage, thumbnailImage, primaryContent, secondaryContent, tertiaryContent, headerBlocks, footerBlocks, mainGrid

### Block List Data Types
- **Naming:** `Test_BlockList_{n}`
- **Configuration:** 3 blocks per data type (1 Simple + 1 Medium + 1 Complex element type)

### Block Grid Data Types
- **Naming:** `Test_BlockGrid_{n}`
- **Configuration:** 30 blocks per data type, 12-column grid

### Templates
Templates are auto-generated with full property rendering:

- **Simple Template:** Renders title, description, isPublished
- **Medium Template:** Renders text properties, related content link, Block List with dynamic property iteration
- **Complex Template:** Renders all sections (Content, Media, Relations, Header Blocks, Footer Blocks, Main Grid)

### Execution Order
1. Load built-in data types (Textstring, Textarea, Boolean, etc.)
2. Create Element Types (Simple, Medium, Complex)
3. Create Block List data types (with 3 element types each)
4. Create Block Grid data types (with 30 element types each)
5. Create Variant Document Types with Templates
6. Create Invariant Document Types with Templates

### Dependencies
- **DataTypeSeeder** - Requires base data types

### Services Used
- `IContentTypeService` - For creating content types
- `IDataTypeService` - For accessing data types
- `IFileService` - For creating templates

---

## 6. MediaSeeder

**File:** `DummyDataSeeder/MediaSeeder.cs`

### Purpose
Seeds media items (PDFs, images, videos) organized in folders.

### Configuration
```json
{
  "Media": {
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
  }
}
```

### Folder Structure
```
Test_PDFs/
├── PDFs_Folder_1/
│   ├── TestPDF_1.pdf
│   ├── TestPDF_2.pdf
│   └── ...
├── PDFs_Folder_2/
└── ...

Test_Images/
├── PNG/
│   ├── PNG_Folder_1/
│   │   ├── TestImage_1.png
│   │   └── ...
│   └── ...
├── JPG/
│   ├── JPG_Folder_1/
│   │   ├── TestImage_1.jpg
│   │   └── ...
│   └── ...

Test_Videos/
├── TestVideo_1.mp4
├── TestVideo_2.mp4
└── ...
```

### File Generation

#### PDFs
- Minimal valid PDF with text content
- Content: "Test PDF {index}"

#### Images (PNG/JPG)
- 100x100 pixel images
- Unique color per image (seeded random)
- Generated using SixLabors.ImageSharp

#### Videos
- MP4 placeholder files with valid header
- ~100KB each (not playable, just valid file signature)

### Dependencies
- **DocumentTypeSeeder** - Runs after to ensure proper order

### Services Used
- `IMediaService` - For creating media items
- `IMediaTypeService` - For getting media types
- `MediaFileManager` - For file storage

---

## 7. ContentSeeder

**File:** `DummyDataSeeder/ContentSeeder.cs`

### Purpose
Seeds content nodes with multi-level hierarchy, variant content support, and domain assignment.

### Configuration
```json
{
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
```

### Content Hierarchy
```
Test_Section_1/                    (Level 1 - Root)
├── Category_1_1/                  (Level 2)
│   ├── Page_1_1_1                 (Level 3)
│   ├── Page_1_1_2
│   │   ├── Detail_1_1_2_1         (Level 4)
│   │   └── Detail_1_1_2_2
│   └── ...
├── Category_1_2/
└── ...
Test_Section_2/
└── ...
```

### Content Distribution
- **Simple Content (50%):** Uses simple document types
- **Medium Content (30%):** Uses medium document types with 1 simple block
- **Complex Content (20%):** Uses complex document types with 1 simple + 1 medium + 1 complex block

### Variant Content Support
For Document Types with `ContentVariation.Culture`:

1. **Culture Names:** Set for ALL languages
   - Format: `{name} ({culture})`
   - Example: `Test_Section_1 (en-US)`, `Test_Section_1 (fr-FR)`

2. **Property Values:** Set for EACH culture separately
   - Each culture gets unique random values

3. **Domain Assignment:** For root content only
   - Creates domain for each language
   - Format: `test-{contentId}-{culture}.localhost`
   - Example: `test-1234-en-us.localhost`

### Block Content Generation

#### Medium Content
- 1 simple block item in `blocks` property

#### Complex Content
- `headerBlocks`: 1 simple + 1 medium + 1 complex block
- `footerBlocks`: 1 simple + 1 medium + 1 complex block
- `mainGrid`: 1 simple + 1 medium + 1 complex block (full width, 12 columns)

### Block JSON Format
```json
{
  "layout": {
    "Umbraco.BlockList": [
      {"contentUdi": "umb://element/{guid}"}
    ]
  },
  "contentData": [
    {
      "contentTypeKey": "{element-type-guid}",
      "udi": "umb://element/{guid}",
      "title": "Lorem ipsum...",
      "description": "..."
    }
  ],
  "settingsData": []
}
```

### Media Picker Values
For complex content with `mainImage` and `thumbnailImage`:
```json
[{"key":"{guid}","mediaKey":"{media-key}"}]
```

### Behavior
- Content is saved as **draft** (not published)
- Skips if content with "Test_" prefix already exists at root
- Loads up to 100 image media items for linking

### Dependencies
- **DocumentTypeSeeder** - Requires document types
- **MediaSeeder** - Requires media items for complex content

### Services Used
- `IContentService` - For creating and saving content
- `IContentTypeService` - For loading document types
- `IDataTypeService` - For block configuration
- `IMediaService` - For loading media items
- `ILocalizationService` - For loading languages
- `IDomainService` - For assigning domains

---

## Seeder Execution Flow

```
┌─────────────────┐
│ LanguageSeeder  │ ← Creates languages
└────────┬────────┘
         │
┌────────▼────────┐
│ DictionarySeeder│ ← Creates dictionary items with translations
└────────┬────────┘
         │
┌────────▼────────┐
│ DataTypeSeeder  │ ← Creates custom data types
└────────┬────────┘
         │
┌────────▼────────┐
│DocumentTypeSeeder│ ← Creates Element Types, Document Types, Templates
└────────┬────────┘
         │
┌────────▼────────┐
│  MediaSeeder    │ ← Creates media items
└────────┬────────┘
         │
┌────────▼────────┐
│   UserSeeder    │ ← Creates test users
└────────┬────────┘
         │
┌────────▼────────┐
│  ContentSeeder  │ ← Creates content with blocks and variant support
└─────────────────┘
```

---

## Common Patterns

### Runtime State Check
All seeders check Umbraco installation status:
```csharp
if (_runtimeState.Level != RuntimeLevel.Run)
{
    Console.WriteLine("Seeder: Skipping - Umbraco is not fully installed yet.");
    return Task.CompletedTask;
}
```

### Idempotency Check
All seeders check for existing data:
```csharp
if (existingItems.Any(x => x.Name.StartsWith("Test_")))
    return;
```

### Configuration Injection
```csharp
public SeederClass(IOptions<SeederConfiguration> config)
{
    _config = config.Value;
}
```
