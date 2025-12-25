using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

public class LanguageSeeder : IHostedService
{
    private readonly ILocalizationService _localizationService;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    // Available cultures pool
    private static readonly string[] AllCultures =
    {
        "en-US","fr-FR","de-DE","es-ES","it-IT",
        "pt-BR","ru-RU","ja-JP","zh-CN","ko-KR",
        "nl-NL","sv-SE","pl-PL","tr-TR","ar-SA",
        "hi-IN","vi-VN","id-ID","th-TH","uk-UA",
        "cs-CZ","da-DK","fi-FI","el-GR","hu-HU",
        "no-NO","ro-RO","sk-SK","sl-SI","bg-BG"
    };

    public LanguageSeeder(
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
            Console.WriteLine("LanguageSeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

        SeedLanguages();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedLanguages()
    {
        var targetCount = _config.Languages.Count;
        var existing = _localizationService.GetAllLanguages().ToList();

        if (existing.Count >= targetCount) return;

        var culturesToCreate = AllCultures.Take(targetCount).ToArray();
        int created = 0;

        foreach (var culture in culturesToCreate)
        {
            if (existing.Any(l => l.IsoCode == culture)) continue;

            var lang = new Language(culture, culture)
            {
                IsDefault = culture == "en-US",
                IsMandatory = culture == "en-US",
                FallbackLanguageId = null
            };
            _localizationService.Save(lang);
            created++;
        }

        Console.WriteLine($"Seeded {created} languages (target: {targetCount}).");
    }
}
