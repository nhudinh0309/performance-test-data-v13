namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
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

    /// <summary>
    /// Scope provider for batched database operations.
    /// </summary>
    protected readonly IScopeProvider ScopeProvider;

    private readonly string _seederTypeName;

    /// <summary>
    /// Creates a new BaseSeeder instance.
    /// </summary>
    protected BaseSeeder(
        ILogger<TSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context,
        IScopeProvider scopeProvider)
    {
        Logger = logger;
        RuntimeState = runtimeState;
        Config = config.Value;
        Options = options.Value;
        Context = context;
        ScopeProvider = scopeProvider;
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
    /// Tracks the last logged percentage per item type to avoid duplicate logs.
    /// Using dictionary allows tracking multiple concurrent operations correctly.
    /// </summary>
    private readonly Dictionary<string, int> _lastLoggedPercentByType = new();

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

        // Get last logged percent for this specific item type
        _lastLoggedPercentByType.TryGetValue(itemType, out var lastLoggedPercent);

        // Reset tracking for new operations (when starting from 1)
        if (current == 1)
        {
            lastLoggedPercent = -1;
        }

        // Always log first item
        if (current == 1)
        {
            Logger.LogInformation("{SeederName}: Created {Current}/{Total} {ItemType} ({Percent}%)",
                SeederName, current, total, itemType, percent);
            _lastLoggedPercentByType[itemType] = percent;
            return;
        }

        // Always log last item
        if (current == total)
        {
            Logger.LogInformation("{SeederName}: Created {Current}/{Total} {ItemType} ({Percent}%)",
                SeederName, current, total, itemType, percent);
            _lastLoggedPercentByType[itemType] = percent;
            return;
        }

        // Log at configured intervals (using threshold-based approach)
        if (interval > 0)
        {
            // Calculate the interval bucket for current and last logged
            int currentBucket = percent / interval;
            int lastBucket = lastLoggedPercent / interval;

            // Log if we've crossed into a new bucket
            if (currentBucket > lastBucket && percent > 0)
            {
                Logger.LogInformation("{SeederName}: Created {Current}/{Total} {ItemType} ({Percent}%)",
                    SeederName, current, total, itemType, percent);
                _lastLoggedPercentByType[itemType] = percent;
            }
        }
    }

    /// <summary>
    /// Get a configured prefix for naming seeded items.
    /// </summary>
    /// <param name="prefixType">The type of prefix to retrieve.</param>
    /// <returns>The configured prefix string.</returns>
    protected string GetPrefix(PrefixType prefixType)
    {
        return prefixType switch
        {
            PrefixType.DataType => Options.Prefixes.DataType,
            PrefixType.ElementType => Options.Prefixes.ElementType,
            PrefixType.VariantDocType => Options.Prefixes.VariantDocType,
            PrefixType.InvariantDocType => Options.Prefixes.InvariantDocType,
            PrefixType.Media => Options.Prefixes.Media,
            PrefixType.Content => Options.Prefixes.Content,
            PrefixType.User => Options.Prefixes.User,
            PrefixType.Dictionary => Options.Prefixes.Dictionary,
            _ => throw new ArgumentOutOfRangeException(nameof(prefixType), prefixType, "Unknown prefix type")
        };
    }

    /// <summary>
    /// Creates a database scope for batched operations with optional notification suppression.
    /// Use this to wrap multiple save operations for better performance.
    /// </summary>
    /// <param name="suppressNotifications">If true, suppresses Umbraco notifications during the scope (faster).</param>
    /// <returns>A scope that must be completed and disposed.</returns>
    protected IScope CreateScopedBatch(bool suppressNotifications = true)
    {
        var scope = ScopeProvider.CreateScope();
        if (suppressNotifications)
        {
            scope.Notifications.Suppress();
        }
        return scope;
    }

    /// <summary>
    /// Returns true if data should be persisted to database.
    /// Returns false when in DryRun mode (only logs what would be created).
    /// </summary>
    protected bool ShouldPersist => !Options.DryRun;

    /// <summary>
    /// Returns true if running in DryRun mode.
    /// </summary>
    protected bool IsDryRun => Options.DryRun;

    /// <summary>
    /// Logs a dry-run message indicating what would be created.
    /// </summary>
    /// <param name="entityType">Type of entity (e.g., "Content", "Media")</param>
    /// <param name="name">Name of the entity</param>
    /// <param name="details">Optional additional details</param>
    protected void LogDryRun(string entityType, string name, string? details = null)
    {
        if (IsDryRun)
        {
            var message = details != null
                ? $"[DRY-RUN] Would create {entityType}: {name} ({details})"
                : $"[DRY-RUN] Would create {entityType}: {name}";
            Logger.LogInformation(message);
        }
    }
}
