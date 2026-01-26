namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// Abstract base class for all seeders. Provides common functionality for
/// logging, error handling, progress reporting, and runtime state checking.
/// </summary>
/// <typeparam name="TSeeder">The concrete seeder type (for logging)</typeparam>
public abstract class BaseSeeder<TSeeder> : ISeeder where TSeeder : class
{
    /// <summary>
    /// Logger instance for this seeder.
    /// </summary>
    protected readonly ILogger<TSeeder> Logger;

    /// <summary>
    /// Umbraco runtime state for checking if Umbraco is fully installed.
    /// </summary>
    protected readonly IRuntimeState RuntimeState;

    /// <summary>
    /// Seeder configuration (target counts, etc.).
    /// </summary>
    protected readonly SeederConfiguration Config;

    /// <summary>
    /// Seeder options (enable flags, prefixes, etc.).
    /// </summary>
    protected readonly SeederOptions Options;

    /// <summary>
    /// Shared execution context with caches and shared Random/Faker.
    /// </summary>
    protected readonly SeederExecutionContext Context;

    private readonly string _seederTypeName;

    /// <summary>
    /// Creates a new BaseSeeder instance.
    /// </summary>
    protected BaseSeeder(
        ILogger<TSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
    {
        Logger = logger;
        RuntimeState = runtimeState;
        Config = config.Value;
        Options = options.Value;
        Context = context;
        _seederTypeName = typeof(TSeeder).Name;
    }

    /// <inheritdoc />
    public abstract int ExecutionOrder { get; }

    /// <inheritdoc />
    public abstract string SeederName { get; }

    /// <summary>
    /// Check if this seeder should execute based on configuration.
    /// </summary>
    protected abstract bool ShouldExecute();

    /// <summary>
    /// Check if data has already been seeded (for idempotency).
    /// </summary>
    protected abstract bool IsAlreadySeeded();

    /// <summary>
    /// Perform the actual seeding work.
    /// </summary>
    protected abstract Task SeedAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Check if master switch is enabled
        if (!Options.Enabled)
        {
            Logger.LogDebug("{SeederName}: Skipping - PerformanceTestDataSeeder is disabled globally", SeederName);
            return;
        }

        // Check if this specific seeder is enabled
        if (!ShouldExecute())
        {
            Logger.LogInformation("{SeederName}: Skipping - disabled in configuration", SeederName);
            return;
        }

        // Check if Umbraco is fully installed
        if (RuntimeState.Level != RuntimeLevel.Run)
        {
            Logger.LogInformation("{SeederName}: Skipping - Umbraco is not fully installed (Level: {Level})",
                SeederName, RuntimeState.Level);
            return;
        }

        // Check if already seeded (idempotency)
        if (IsAlreadySeeded())
        {
            Logger.LogInformation("{SeederName}: Skipping - data already seeded", SeederName);
            return;
        }

        Logger.LogInformation("{SeederName}: Starting seeding process (Seed: {Seed})...",
            SeederName, Context.Seed);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await SeedAsync(cancellationToken);
            stopwatch.Stop();

            Logger.LogInformation("{SeederName}: Completed successfully in {ElapsedMs}ms",
                SeederName, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Logger.LogWarning("{SeederName}: Seeding was cancelled after {ElapsedMs}ms",
                SeederName, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "{SeederName}: Failed after {ElapsedMs}ms - {Message}",
                SeederName, stopwatch.ElapsedMilliseconds, ex.Message);

            if (Options.StopOnError)
            {
                throw;
            }
            // If StopOnError is false, log and continue to next seeder
        }
    }

    /// <summary>
    /// Log progress at configured intervals.
    /// </summary>
    /// <param name="current">Current item number</param>
    /// <param name="total">Total items to process</param>
    /// <param name="itemType">Type of item being processed (for logging)</param>
    protected void LogProgress(int current, int total, string itemType)
    {
        if (total <= 0) return;

        var percent = (int)((current / (double)total) * 100);
        var interval = Options.ProgressIntervalPercent;

        // Always log first, last, and at configured intervals
        if (current == 1 || current == total || (interval > 0 && percent % interval == 0 && percent > 0))
        {
            // Avoid duplicate logs at same percentage
            var prevPercent = (int)(((current - 1) / (double)total) * 100);
            if (current == 1 || current == total || prevPercent / interval != percent / interval)
            {
                Logger.LogInformation("{SeederName}: Created {Current}/{Total} {ItemType} ({Percent}%)",
                    SeederName, current, total, itemType, percent);
            }
        }
    }

    /// <summary>
    /// Get a configured prefix for naming seeded items.
    /// </summary>
    protected string GetPrefix(string prefixType)
    {
        return prefixType.ToLowerInvariant() switch
        {
            "datatype" => Options.Prefixes.DataType,
            "elementtype" => Options.Prefixes.ElementType,
            "variantdoctype" => Options.Prefixes.VariantDocType,
            "invariantdoctype" => Options.Prefixes.InvariantDocType,
            "media" => Options.Prefixes.Media,
            "content" => Options.Prefixes.Content,
            "user" => Options.Prefixes.User,
            "dictionary" => Options.Prefixes.Dictionary,
            _ => "Test_"
        };
    }
}
