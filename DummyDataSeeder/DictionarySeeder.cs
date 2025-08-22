using Bogus;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Models;

public class DictionarySeeder : IHostedService
{
    private readonly ILocalizationService _localizationService;

    public DictionarySeeder(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SeedDictionaries();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedDictionaries()
    {
        // if have any root dictionary items, skip seeding
        if (_localizationService.GetRootDictionaryItems().Any()) return;

        var faker = new Faker("en");
        var langs = _localizationService.GetAllLanguages().ToList();

        int total = 0;

        // create 3 folder levels: Root/Section/Item
        for (int i = 0; i < 10; i++) // 10 root folders
        {
            var root = _localizationService.CreateDictionaryItemWithIdentity($"Root_{i}", null);
            _localizationService.Save(root);

            for (int j = 0; j < 5; j++) // 5 sections each root
            {
                var section = _localizationService.CreateDictionaryItemWithIdentity($"Section_{i}_{j}", root.Key);
                _localizationService.Save(section);

                for (int k = 0; k < 30; k++) // 30 items each section
                {
                    var item = _localizationService.CreateDictionaryItemWithIdentity($"Dict_{i}_{j}_{k}", section.Key);

                    var translations = new List<DictionaryTranslation>();
                    foreach (var lang in langs)
                    {
                        var value = faker.Lorem.Word();
                        translations.Add(new DictionaryTranslation(lang, value));
                    }
                    item.Translations = translations;

                    _localizationService.Save(item);

                    total++;
                }
            }
        }

        Console.WriteLine($"✅ Seeded {total} dictionary items with {langs.Count} translations each.");
    }
}
