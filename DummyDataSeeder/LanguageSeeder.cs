using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

public class LanguageSeeder : IHostedService
{
    private readonly ILocalizationService _localizationService;

    public LanguageSeeder(ILocalizationService  localizationService)
    {
        _localizationService = localizationService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SeedLanguages();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedLanguages()
    {
        var existing = _localizationService.GetAllLanguages().ToList();
        if (existing.Count >= 20) return;

        var cultures = new[]
        {
            "en-US","fr-FR","de-DE","es-ES","it-IT",
            "pt-BR","ru-RU","ja-JP","zh-CN","ko-KR",
            "nl-NL","sv-SE","pl-PL","tr-TR","ar-SA",
            "hi-IN","vi-VN","id-ID","th-TH","uk-UA"
        };

        foreach (var culture in cultures)
        {
            if (existing.Any(l => l.IsoCode == culture)) continue;

            var lang = new Language(culture, culture)
            {
                IsDefault = culture == "en-US",
                IsMandatory = culture == "en-US",
                FallbackLanguageId = null
            };
            _localizationService.Save(lang);
        }
    }
}
