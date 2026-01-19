namespace Umbraco.Community.DummyDataSeeder.Composing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.DummyDataSeeder.Configuration;
using Umbraco.Community.DummyDataSeeder.Infrastructure;
using Umbraco.Community.DummyDataSeeder.Seeders;

/// <summary>
/// Composer for registering DummyDataSeeder services with Umbraco.
/// This is automatically discovered and executed by Umbraco during startup.
/// </summary>
public class DummyDataSeederComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        // Bind configuration from appsettings.json
        builder.Services.Configure<SeederConfiguration>(
            builder.Config.GetSection(SeederConfiguration.SectionName));

        builder.Services.Configure<SeederOptions>(
            builder.Config.GetSection(SeederOptions.SectionName));

        // Register shared execution context as singleton
        // This provides shared Faker, Random, and caches across all seeders
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeederOptions>>().Value;
            return new SeederExecutionContext(options.FakerSeed);
        });

        // Register configuration validator
        builder.Services.AddSingleton<SeederConfigurationValidator>();

        // Register all seeders
        // Order is determined by ExecutionOrder property, not registration order
        builder.Services.AddTransient<ISeeder, LanguageSeeder>();
        builder.Services.AddTransient<ISeeder, DictionarySeeder>();
        builder.Services.AddTransient<ISeeder, DataTypeSeeder>();
        builder.Services.AddTransient<ISeeder, DocumentTypeSeeder>();
        builder.Services.AddTransient<ISeeder, MediaSeeder>();
        builder.Services.AddTransient<ISeeder, ContentSeeder>();
        builder.Services.AddTransient<ISeeder, UserSeeder>();

        // Register orchestrator as hosted service
        // This runs all seeders in order during application startup
        builder.Services.AddHostedService<SeederOrchestrator>();
    }
}
