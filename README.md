# Umbraco.Community.PerformanceTestDataSeeder

A configurable dummy data seeder for Umbraco CMS v17+ designed for performance and load testing.

## Features

- Seeds languages, dictionary items, data types, document types, media, content, users, and members
- **Hierarchical document types** - dedicated Section, Category, Page, and Detail doc types with Collection (List View) and allowed child node configuration
- **Member login system** - seeded members with passwords, login/member area pages, and API endpoints for load testing authentication flows
- **Contact form** - rendered contact page with submissions persisted to the Umbraco database for verifying full pipeline under load
- **Nested blocks support** - configurable depth of blocks within blocks for realistic load testing
- Fully configurable via `appsettings.json`
- Reproducible test data with configurable Faker seed
- Enable/disable individual seeders
- Idempotent - safe to run multiple times
- Structured logging with progress reporting and per-seeder timing summary
- Auto-registration via Umbraco IComposer
- Requires Umbraco to be fully installed before seeding (skips silently during install)

## Installation

```bash
dotnet add package Umbraco.Community.PerformanceTestDataSeeder
```

That's it! The package automatically registers with Umbraco and runs on startup.

## Local Development

To test the package locally before publishing:

1. **Add a project reference** to your Umbraco project's `.csproj`:
   ```xml
   <ProjectReference Include="..\path\to\Umbraco.Community.PerformanceTestDataSeeder.csproj" />
   ```

2. **Add configuration** to `appsettings.json`:
   ```json
   {
     "PerformanceTestDataSeeder": {
       "Options": {
         "Enabled": true,
         "Preset": "Small",
         "StopOnError": true
       }
     }
   }
   ```

3. **Run the Umbraco project**:
   ```bash
   dotnet run
   ```

4. **Check the status endpoint** to verify seeding completed:
   ```
   GET /umbraco/api/seederstatus/status
   ```

## Building & Packaging

To create a NuGet package for distribution:

```bash
# Build the package
dotnet pack -c Release

# The .nupkg file will be in bin/Release/
```

To publish to NuGet.org:

```bash
# Push to NuGet (requires API key)
dotnet nuget push bin/Release/Umbraco.Community.PerformanceTestDataSeeder.2.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

For local testing with a NuGet package (instead of project reference):

```bash
# Create a local NuGet source folder
mkdir ~/local-nugets

# Pack and copy to local source
dotnet pack -c Release -o ~/local-nugets

# Add local source to your test project's NuGet.config or use:
dotnet add package Umbraco.Community.PerformanceTestDataSeeder --source ~/local-nugets
```

## Quick Start with Presets

The easiest way to get started is using a preset. Add this to your `appsettings.json`:

```json
{
  "PerformanceTestDataSeeder": {
    "Options": {
      "Enabled": true,
      "Preset": "Medium"
    }
  }
}
```

### Available Presets

| Preset | Languages | Content | Media | Users | Members | NestingDepth | Total Items |
|--------|-----------|---------|-------|-------|---------|--------------|-------------|
| `Small` | 3 | 50 | 30 | 5 | 10 | 2 | ~200 |
| `Medium` | 10 | 500 | 405 | 20 | 50 | 4 | ~2,000 |
| `Large` | 20 | 10,000 | 4,020 | 50 | 100 | 6 | ~25,000 |
| `Massive` | 30 | 50,000 | 16,050 | 100 | 500 | 8 | ~100,000 |
| `Custom` | - | - | - | - | - | - | Use SeederConfiguration |

When using a preset, you don't need to specify the full `SeederConfiguration` section.

### Content Domains

The seeder assigns path-based domains to each root content node for multi-language routing. Domains are created as `{DomainSuffix}/test-{contentId}-{culture}`.

> **Important:** For local development, `DomainSuffix` **must** include the port your site runs on. Without it, Umbraco's routing won't match incoming requests and domains won't work.

```json
{
  "PerformanceTestDataSeeder": {
    "Options": {
      "DomainSuffix": "localhost:44340"
    }
  }
}
```

This produces domains like `localhost:44340/test-1158-en-us`, accessible at `https://localhost:44340/test-1158-en-us/`.

For Azure, use your app hostname (e.g., `"myapp.azurewebsites.net"`).

To skip domain creation entirely, set `"SkipContentDomains": true`.

## Custom Configuration

For fine-grained control, set `Preset` to `Custom` (or omit it) and add configuration to your `appsettings.json`:

```json
{
  "SeederConfiguration": {
    "Languages": { "Count": 20 },
    "Dictionary": {
      "RootFolders": 10,
      "SectionsPerRoot": 5,
      "ItemsPerSection": 30
    },
    "Users": { "Count": 30 },
    "Members": {
      "Count": 30,
      "DefaultPassword": "Test1234!"
    },
    "DataTypes": {
      "ListView": 30,
      "MultiNodeTreePicker": 40,
      "RichTextEditor": 10,
      "MediaPicker": 10,
      "Textarea": 10,
      "Dropdown": 10,
      "Numeric": 10
    },
    "DocumentTypes": {
      "ElementTypes": { "Simple": 65, "Medium": 39, "Complex": 26 },
      "VariantDocTypes": { "Simple": 55, "Medium": 33, "Complex": 22 },
      "InvariantDocTypes": { "Simple": 20, "Medium": 12, "Complex": 8 },
      "BlockList": 40,
      "BlockGrid": 60,
      "NestingDepth": 4
    },
    "Media": {
      "PDF": { "Count": 200, "FolderCount": 10 },
      "PNG": { "Count": 200, "FolderCount": 10 },
      "JPG": { "Count": 200, "FolderCount": 10 },
      "Video": { "Count": 10, "FolderCount": 1 }
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
  },
  "PerformanceTestDataSeeder": {
    "Options": {
      "Preset": "Custom",
      "Enabled": true,
      "StopOnError": false,
      "FakerSeed": 12345,
      "ProgressIntervalPercent": 10,
      "EnabledSeeders": {
        "Languages": true,
        "Dictionary": true,
        "DataTypes": true,
        "DocumentTypes": true,
        "Media": true,
        "Content": true,
        "Users": true,
        "Members": true,
        "ContactForm": true
      },
      "Prefixes": {
        "DataType": "Test_",
        "ElementType": "testElement",
        "VariantDocType": "testVariant",
        "InvariantDocType": "testInvariant",
        "Media": "Test_",
        "Content": "Test_",
        "User": "TestUser_",
        "Dictionary": "Dict_",
        "Member": "TestMember_",
        "ParentDocType": "testParent"
      }
    }
  }
}
```

## Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Preset` | enum | Custom | Predefined dataset size (Small, Medium, Large, Massive, Custom) |
| `Enabled` | bool | false | Master switch - must be explicitly enabled |
| `StopOnError` | bool | false | Stop seeding if any seeder fails |
| `FakerSeed` | int? | null | Seed for reproducible test data |
| `ProgressIntervalPercent` | int | 10 | Log progress every N percent |
| `EnabledSeeders.*` | bool | true | Enable/disable individual seeders |
| `Prefixes.*` | string | various | Configurable naming prefixes |
| `CustomCultures` | string[]? | null | Custom cultures (overrides default 30) |
| `BatchSize` | int | 50 | Items per database batch |
| `ParallelDegree` | int | 4 | Max parallelism for CPU-bound operations |
| `PublishMode` | enum | FirstSection | Content publishing mode (None, All, FirstSection) |
| `PublishBatchSize` | int | 50 | Items per publish batch |
| `DryRun` | bool | false | Log what would be created without persisting |
| `DomainSuffix` | string | "localhost" | Domain host for content domains (e.g., "localhost:44340") |
| `SkipContentDomains` | bool | false | Skip creating content domains entirely |
| `RebuildCacheAfterSeeding` | bool | true | Rebuild published cache after seeding |
| `Members.Count` | int | 30 | Number of test members to seed |
| `Members.DefaultPassword` | string | "Test1234!" | Password for all test members (must meet Umbraco password policy) |

## Seeder Execution Order

1. **LanguageSeeder** - Creates languages from culture pool
2. **DictionarySeeder** - Creates dictionary items with translations
3. **DataTypeSeeder** - Creates custom data types
4. **DocumentTypeSeeder** - Creates element types, nested container elements, page doc types (with Collection), detail doc types (leaf nodes), parent doc types (Section/Category with Collection and allowed children), block types, and templates
5. **MediaSeeder** - Creates media items (PDFs, images, videos)
6. **ContentSeeder** - Creates content hierarchy using dedicated doc types per level (Section → Category → Page → Detail)
7. **UserSeeder** - Creates test users
8. **MemberSeeder** - Creates front-end members with passwords, member groups, login page, and member area page
9. **ContactFormSeeder** - Creates a contact form page with submission endpoints

## Default Data Quantities

| Category | Default Count |
|----------|---------------|
| Languages | 20 |
| Dictionary Items | 1,500 |
| Data Types | 130 |
| Element Types | 130 |
| Document Types | 110 + 6 (parent/detail) |
| Templates | 116 |
| Block List/Grid Types | 100 |
| Media Items | 610 |
| Content Nodes | 300 |
| Users | 30 |
| Members | 30 |
| Frontend Pages | 3 (login, member area, contact form) |

## Nested Blocks

The seeder creates nested block structures to simulate real-world content complexity. The `NestingDepth` setting controls how deep blocks can be nested.

**Example with NestingDepth = 2:**
```
BlockList
  └── NestedBlock_Depth1
        └── nestedBlocks (BlockList)
              └── Simple/Medium/Complex Elements
```

**Example with NestingDepth = 4:**
```
BlockList
  └── NestedBlock_Depth1
        └── nestedBlocks → NestedBlock_Depth2
                            └── nestedBlocks → NestedBlock_Depth3
                                                └── nestedBlocks → Simple/Medium/Complex Elements
```

This creates realistic load testing scenarios where:
- JSON serialization/deserialization is stressed
- Backoffice rendering performance is tested
- Content API handles deeply nested structures

## Content Hierarchy & Document Types

The seeder creates a structured content tree with dedicated document types per level:

```
Section (root)          → testParentSection{Variant|Invariant}
  └── Category          → testParentCategory{Variant|Invariant}
        └── Page        → testVariant/Invariant{Simple|Medium|Complex}{N}
              └── Detail → testParentDetail{Variant|Invariant}
```

| Level | Doc Type | Collection (List View) | Allowed Children |
|-------|----------|:----------------------:|------------------|
| **Section** | `testParentSection*` | Yes | Category only |
| **Category** | `testParentCategory*` | Yes | All page doc types |
| **Page** | `testVariant/Invariant*` | Yes | Detail only |
| **Detail** | `testParentDetail*` | No | None |

- **Section** and **Category** doc types are container nodes with the built-in "List View - Content" collection configured, so child nodes display in list view in the backoffice.
- **Page** doc types (simple/medium/complex, variant/invariant) also have collection view enabled and allow Detail doc types as children.
- **Detail** doc types are leaf nodes with no collection and no allowed children.
- Each level uses both variant (culture-varying) and invariant versions for realistic testing.

## Reproducible Test Data

Set `FakerSeed` to a fixed value to generate the same test data across runs:

```json
{
  "PerformanceTestDataSeeder": {
    "Options": {
      "FakerSeed": 12345
    }
  }
}
```

## Disable Specific Seeders

```json
{
  "PerformanceTestDataSeeder": {
    "Options": {
      "EnabledSeeders": {
        "Users": false,
        "Dictionary": false
      }
    }
  }
}
```

## Status Endpoint (for Automation)

Poll the status endpoint to know when seeding is complete:

```
GET /umbraco/api/seederstatus/status
```

**Response:**
```json
{
  "status": "Completed",
  "isComplete": true,
  "currentSeeder": null,
  "executedCount": 9,
  "failedCount": 0,
  "elapsedMs": 45230,
  "errorMessage": null
}
```

**HTTP Status Codes:**
- `200` - Seeding complete (Completed, CompletedWithErrors, or Skipped)
- `202` - Seeding in progress (Running or NotStarted)
- `503` - Seeding failed

**Automation example (bash):**
```bash
# Wait for seeding to complete
while true; do
  response=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/umbraco/api/seederstatus/status)
  if [ "$response" = "200" ]; then
    echo "Seeding complete!"
    break
  elif [ "$response" = "503" ]; then
    echo "Seeding failed!"
    exit 1
  fi
  sleep 10
done
```

## Load Testing Endpoints

The seeder creates frontend pages and API endpoints designed for load testing with tools like k6.

### Member Configuration Endpoint

Fetch all member credentials and URLs in one call:

```
GET /umbraco/api/seederstatus/members
```

**Response:**
```json
{
  "prefix": "TestMember_",
  "count": 30,
  "password": "Test1234!",
  "emailDomain": "example.com",
  "loginUrl": "/umbraco/api/memberauth/login",
  "logoutUrl": "/umbraco/api/memberauth/logout",
  "meUrl": "/umbraco/api/memberauth/me",
  "contactFormUrl": "/umbraco/api/contactform/submit",
  "contactFormStatsUrl": "/umbraco/api/contactform/stats",
  "loginPageUrl": "/testmember-member-login/",
  "memberAreaPageUrl": "/testmember-member-area/",
  "contactFormPageUrl": "/contact-us/"
}
```

### Member Authentication (JSON API)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/umbraco/api/memberauth/login` | POST | Authenticate with `{ "username": "...", "password": "..." }`, returns JSON + auth cookie |
| `/umbraco/api/memberauth/logout` | POST | Sign out, clears auth cookie |
| `/umbraco/api/memberauth/me` | GET | Returns current member info |

### Member Login (Frontend)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/umbraco/api/memberlogin/login` | POST | Form login, redirects to member area on success |
| `/umbraco/api/memberlogin/logout` | POST | Form logout, redirects to login page |

### Contact Form

Each submission is saved as a content node under "Contact Submissions" in the Umbraco content tree, proving the full pipeline (form → controller → database) works under load.

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/umbraco/api/contactform/submit` | POST | JSON submission `{ "name", "email", "subject", "message" }`, saved to DB |
| `/umbraco/api/contactform/form-submit` | POST | Form submission with redirect, saved to DB |
| `/umbraco/api/contactform/stats` | GET | Returns `{ "totalSubmissions": N }` counted from DB |

### Frontend Pages

The seeder automatically creates and publishes these Umbraco content pages:

| Page | URL | Purpose |
|------|-----|---------|
| Member Login | `/{prefix}member-login/` | Login form for members |
| Member Area | `/{prefix}member-area/` | Shows member info when authenticated |
| Contact Us | `/contact-us/` | Contact form with name, email, subject, message |

### k6 Example

```javascript
import http from 'k6/http';
import { check } from 'k6';

export function setup() {
  const res = http.get('https://localhost:44340/umbraco/api/seederstatus/members');
  return JSON.parse(res.body);
}

export default function (config) {
  // Pick a random member
  const i = Math.floor(Math.random() * config.count) + 1;
  const username = `${config.prefix}${i}`;

  // Login
  const loginRes = http.post(`https://localhost:44340${config.loginUrl}`,
    JSON.stringify({ username, password: config.password }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  check(loginRes, { 'login succeeded': (r) => r.status === 200 });

  // Verify authenticated
  const meRes = http.get(`https://localhost:44340${config.meUrl}`);
  check(meRes, { 'is authenticated': (r) => JSON.parse(r.body).success === true });

  // Submit contact form
  const formRes = http.post(`https://localhost:44340${config.contactFormUrl}`,
    JSON.stringify({ name: 'Test', email: 'test@example.com', subject: 'Load Test', message: 'Hello' }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  check(formRes, { 'form submitted': (r) => r.status === 200 });

  // Logout
  http.post(`https://localhost:44340${config.logoutUrl}`);
}
```

## Performance Considerations

- **Database**: SQL Server is recommended for large datasets. SQLite may timeout with Large/Massive presets.
- **Time estimates** (with default `PublishMode: FirstSection`):
  - Small: ~10 seconds
  - Medium: ~1-2 minutes
  - Large: ~5-10 minutes
  - Massive: ~30-40 minutes
- **Memory**: Large media generation requires adequate RAM. Consider reducing media counts if memory constrained.
- **Disk space**: Media seeder generates actual files. Massive preset creates ~16,000+ media files.

## Requirements

- **Umbraco CMS 17+** (17.0.0 and up)
- .NET 10.0+

## Compatibility

This package is designed as a **class library** that references `Umbraco.Cms.Core`, `Umbraco.Cms.Infrastructure`, and `Umbraco.Cms.Web.Common` (not the full Umbraco.Cms metapackage). This means:

- It has minimal web project dependencies
- It can be easily added to any existing Umbraco project
- It will automatically register via the IComposer pattern

### Version Support

| Package Version | Umbraco Version |
|-----------------|-----------------|
| 1.0.x           | 13.0.0+         |
| 2.0.x           | 17.0.0+         |

> **Note:** The 2.0.x branch uses Umbraco 17 async APIs and is not compatible with earlier versions. Use 1.0.x for Umbraco 13/14.

## Dependencies

- Bogus (fake data generation)
- SixLabors.ImageSharp (image generation)
- Umbraco.Cms.Core (17.x)
- Umbraco.Cms.Infrastructure (17.x)
- Umbraco.Cms.Web.Common (17.x)

## License

MIT
