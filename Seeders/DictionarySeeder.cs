namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Seeds dictionary items with translations for all languages.
/// Execution order: 2 (depends on LanguageSeeder).
/// </summary>
public class DictionarySeeder : BaseSeeder<DictionarySeeder>
{
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates a new DictionarySeeder instance.
    /// </summary>
    public DictionarySeeder(
        ILocalizationService localizationService,
        IScopeProvider scopeProvider,
        ILogger<DictionarySeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
    {
        _localizationService = localizationService;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 2;

    /// <inheritdoc />
    public override string SeederName => "DictionarySeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.Dictionary;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        // Check if we have any root dictionary items with our prefix
        var prefix = GetPrefix(PrefixType.Dictionary);
        var roots = _localizationService.GetRootDictionaryItems();
        return roots?.Any(r => r.ItemKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        // Ensure we have languages loaded
        if (Context.Languages.Count == 0)
        {
            Context.SetLanguages(_localizationService.GetAllLanguages());
        }

        var langs = Context.Languages;
        if (langs.Count == 0)
        {
            Logger.LogWarning("No languages available. Dictionary items require at least one language for translations. Skipping dictionary seeding.");
            return Task.CompletedTask;
        }

        Logger.LogDebug("Creating dictionary items with translations for {Count} languages", langs.Count);

        var rootFolders = Config.Dictionary.RootFolders;
        var sectionsPerRoot = Config.Dictionary.SectionsPerRoot;
        var itemsPerSection = Config.Dictionary.ItemsPerSection;
        var totalTarget = Config.Dictionary.TotalItems;
        var prefix = GetPrefix(PrefixType.Dictionary);

        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create {Total} dictionary items: {Roots} roots × {Sections} sections × {Items} items",
                totalTarget, rootFolders, sectionsPerRoot, itemsPerSection);
            Logger.LogInformation("[DRY-RUN] Each item would have {Count} translations", langs.Count);
            return Task.CompletedTask;
        }

        int totalCreated = 0;
        int rootCreated = 0;

        for (int i = 0; i < rootFolders; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Use a scope per root folder for batching
                using var scope = CreateScopedBatch();

                var rootKey = $"{prefix}Root_{i}";
                var root = _localizationService.CreateDictionaryItemWithIdentity(rootKey, null);
                _localizationService.Save(root);
                rootCreated++;

                for (int j = 0; j < sectionsPerRoot; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sectionKey = $"{prefix}Section_{i}_{j}";
                    var section = _localizationService.CreateDictionaryItemWithIdentity(sectionKey, root.Key);
                    _localizationService.Save(section);

                    for (int k = 0; k < itemsPerSection; k++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var itemKey = $"{prefix}{i}_{j}_{k}";
                        var item = _localizationService.CreateDictionaryItemWithIdentity(itemKey, section.Key);

                        // Add translations for all languages
                        var translations = new List<IDictionaryTranslation>();
                        foreach (var lang in langs)
                        {
                            var value = Context.Faker.Lorem.Word();
                            translations.Add(new DictionaryTranslation(lang, value));
                        }
                        item.Translations = translations;

                        _localizationService.Save(item);
                        totalCreated++;
                    }
                }

                scope.Complete();
                LogProgress(rootCreated, rootFolders, "root folders");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create dictionary items for root {Index}", i);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation(
            "Seeded {TotalCreated} dictionary items in {RootFolders} roots with {LangCount} translations each (target: {Target})",
            totalCreated, rootCreated, langs.Count, totalTarget);

        return Task.CompletedTask;
    }
}
