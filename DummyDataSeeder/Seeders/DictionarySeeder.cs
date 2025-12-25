using Bogus;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

public class DictionarySeeder : IHostedService
{
    private readonly ILocalizationService _localizationService;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    public DictionarySeeder(
        ILocalizationService localizationService,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config)
    {
        _localizationService = localizationService;
        _runtimeState = runtimeState;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed when Umbraco is fully installed and running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            Console.WriteLine("DictionarySeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

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

        var rootFolders = _config.Dictionary.RootFolders;
        var sectionsPerRoot = _config.Dictionary.SectionsPerRoot;
        var itemsPerSection = _config.Dictionary.ItemsPerSection;

        int total = 0;

        // create 3 folder levels: Root/Section/Item
        for (int i = 0; i < rootFolders; i++)
        {
            var root = _localizationService.CreateDictionaryItemWithIdentity($"Root_{i}", null);
            _localizationService.Save(root);

            for (int j = 0; j < sectionsPerRoot; j++)
            {
                var section = _localizationService.CreateDictionaryItemWithIdentity($"Section_{i}_{j}", root.Key);
                _localizationService.Save(section);

                for (int k = 0; k < itemsPerSection; k++)
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

            if ((i + 1) % 5 == 0)
                Console.WriteLine($"Created {total} dictionary items...");
        }

        Console.WriteLine($"Seeded {total} dictionary items with {langs.Count} translations each (target: {_config.Dictionary.TotalItems}).");
    }
}
