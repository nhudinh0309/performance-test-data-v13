namespace Umbraco.Community.PerformanceTestDataSeeder.Composing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;
using Umbraco.Community.PerformanceTestDataSeeder.Seeders;

/// <summary>
/// Composer for registering PerformanceTestDataSeeder services with Umbraco.
/// This is automatically discovered and executed by Umbraco during startup.
/// </summary>
public class PerformanceTestDataSeederComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        // Bind configuration from appsettings.json
        // Options must be bound first as SeederConfigurationSetup depends on it
        builder.Services.Configure<SeederOptions>(
            builder.Config.GetSection(SeederOptions.SectionName));

        builder.Services.Configure<SeederConfiguration>(
            builder.Config.GetSection(SeederConfiguration.SectionName));

        // Register post-configuration to apply presets
        builder.Services.ConfigureOptions<SeederConfigurationSetup>();

        // Register shared execution context as singleton
        // This provides shared Faker, Random, and caches across all seeders
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeederOptions>>().Value;
            return new SeederExecutionContext(options.FakerSeed);
        });

        // Register status service for health check endpoint
        builder.Services.AddSingleton<SeederStatusService>();

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

        // Register orchestrator as notification handler
        // This runs all seeders after Umbraco has fully started
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SeederOrchestrator>();
    }
}
