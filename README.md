# Umbraco 13 Performance Test Data Seeder

This project is an **Umbraco CMS v13 site** with custom seeders for generating large amounts of test data.  
It is designed to simulate **enterprise-scale datasets** for **performance and load testing**.

---

## 🚀 Features

- **Languages**: Automatically seeds **20 languages** (e.g., en-US, fr-FR, vi-VN, ja-JP).
- **Dictionary Items**: Seeds **1,500 dictionary items** organized into 3 folder levels, each with translations for all 20 languages.
- (Planned) **Media**: Seed ~23,000 media items (PDF, PNG, JPG, Videos) with 3–4 folder levels.
- (Planned) **Document Types**: Seed 280 document types (simple, medium, complex).
- (Planned) **Content**: Seed 25,000 content nodes with 3–4 levels and different complexity.

---

## 🛠️ Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)  
- [Umbraco CMS 13](https://our.umbraco.com/download) (created via template)  
- SQLite or SQL Server database  
- [Bogus](https://github.com/bchavez/Bogus) NuGet package for generating fake data  

---

## 📂 Project Structure

- `LanguageSeeder.cs` → Seeds 20 languages  
- `DictionarySeeder.cs` → Seeds 1,500 dictionary items with translations  
- `Program.cs` → Registers seeders as hosted services  
- `appsettings.json` → Database and Umbraco configuration  

