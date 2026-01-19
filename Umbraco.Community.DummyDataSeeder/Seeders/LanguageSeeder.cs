namespace Umbraco.Community.DummyDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.DummyDataSeeder.Configuration;
using Umbraco.Community.DummyDataSeeder.Infrastructure;

/// <summary>
/// Seeds languages for multi-language content support.
/// Execution order: 1 (first seeder to run).
/// </summary>
public class LanguageSeeder : BaseSeeder<LanguageSeeder>
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Default pool of cultures to choose from.
    /// </summary>
    private static readonly string[] DefaultCultures =
    {
        "en-US", "fr-FR", "de-DE", "es-ES", "it-IT",
        "pt-BR", "ru-RU", "ja-JP", "zh-CN", "ko-KR",
        "nl-NL", "sv-SE", "pl-PL", "tr-TR", "ar-SA",
        "hi-IN", "vi-VN", "id-ID", "th-TH", "uk-UA",
        "cs-CZ", "da-DK", "fi-FI", "el-GR", "hu-HU",
        "no-NO", "ro-RO", "sk-SK", "sl-SI", "bg-BG"
    };

    /// <summary>
    /// Creates a new LanguageSeeder instance.
    /// </summary>
    public LanguageSeeder(
        ILocalizationService localizationService,
        ILogger<LanguageSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context)
    {
        _localizationService = localizationService;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 1;

    /// <inheritdoc />
    public override string SeederName => "LanguageSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.Languages;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var existing = _localizationService.GetAllLanguages().ToList();
        return existing.Count >= Config.Languages.Count;
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        var targetCount = Config.Languages.Count;
        var existing = _localizationService.GetAllLanguages().ToList();
        var existingCodes = existing.Select(l => l.IsoCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Use custom cultures if configured, otherwise use defaults
        var cultures = Options.CustomCultures ?? DefaultCultures;
        var culturesToCreate = cultures
            .Take(targetCount)
            .Where(c => !existingCodes.Contains(c))
            .ToList();

        if (culturesToCreate.Count == 0)
        {
            Logger.LogInformation("All target languages already exist");
            return Task.CompletedTask;
        }

        int created = 0;
        foreach (var culture in culturesToCreate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var isDefault = culture.Equals("en-US", StringComparison.OrdinalIgnoreCase)
                    && !existing.Any(l => l.IsDefault);

                var lang = new Language(culture, culture)
                {
                    IsDefault = isDefault,
                    IsMandatory = isDefault,
                    FallbackIsoCode = null
                };

                _localizationService.Save(lang);
                created++;

                LogProgress(created, culturesToCreate.Count, "languages");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create language {Culture}", culture);
                if (Options.StopOnError) throw;
            }
        }

        // Cache languages in context for other seeders
        Context.Languages = _localizationService.GetAllLanguages().ToList();

        Logger.LogInformation("Seeded {Created} languages (total: {Total}, target: {Target})",
            created, Context.Languages.Count, targetCount);

        return Task.CompletedTask;
    }
}
