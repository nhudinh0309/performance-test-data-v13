namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;

/// <summary>
/// Notification handler that orchestrates the execution of all seeders in the correct order.
/// Triggered when Umbraco application has fully started.
/// </summary>
public class SeederOrchestrator : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IEnumerable<ISeeder> _seeders;
    private readonly ILogger<SeederOrchestrator> _logger;
    private readonly SeederOptions _options;
    private readonly SeederConfigurationValidator _validator;
    private readonly SeederConfiguration _config;
    private readonly SeederStatusService _statusService;

    /// <summary>
    /// Creates a new SeederOrchestrator instance.
    /// </summary>
    public SeederOrchestrator(
        IEnumerable<ISeeder> seeders,
        ILogger<SeederOrchestrator> logger,
        IOptions<SeederOptions> options,
        IOptions<SeederConfiguration> config,
        SeederConfigurationValidator validator,
        SeederStatusService statusService)
    {
        // Sort seeders by execution order
        _seeders = seeders.OrderBy(s => s.ExecutionOrder).ToList();
        _logger = logger;
        _options = options.Value;
        _config = config.Value;
        _validator = validator;
        _statusService = statusService;
    }

    /// <summary>
    /// Handles the UmbracoApplicationStartedNotification to execute seeders.
    /// </summary>
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("PerformanceTestDataSeeder: Disabled in configuration, skipping all seeders");
            _statusService.SetSkipped();
            return;
        }

        // Log preset being used
        if (_options.Preset != SeederPreset.Custom)
        {
            _logger.LogInformation("PerformanceTestDataSeeder: Using preset '{Preset}'", _options.Preset);
        }

        // Validate configuration (preset has already been applied via SeederConfigurationSetup)
        var validationResult = _validator.Validate(_config);
        if (!validationResult.IsValid)
        {
            _logger.LogError("PerformanceTestDataSeeder: Configuration validation failed:");
            foreach (var error in validationResult.Errors)
            {
                _logger.LogError("  - {Error}", error);
            }

            if (_options.StopOnError)
            {
                throw new InvalidOperationException(
                    $"PerformanceTestDataSeeder configuration is invalid: {string.Join(", ", validationResult.Errors)}");
            }

            _logger.LogWarning("PerformanceTestDataSeeder: Continuing despite validation errors (StopOnError=false)");
        }

        var seederList = _seeders.ToList();
        _logger.LogInformation(
            "PerformanceTestDataSeeder: Starting orchestration of {Count} seeders (FakerSeed: {Seed})",
            seederList.Count,
            _options.FakerSeed?.ToString() ?? "auto");

        _statusService.SetStarted();
        var totalStopwatch = Stopwatch.StartNew();
        var executedCount = 0;
        var failedCount = 0;

        try
        {
            foreach (var seeder in seederList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("PerformanceTestDataSeeder: Orchestration cancelled");
                    break;
                }

                _statusService.SetCurrentSeeder(seeder.SeederName);

                try
                {
                    await seeder.ExecuteAsync(cancellationToken);
                    executedCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "PerformanceTestDataSeeder: Seeder {SeederName} failed", seeder.SeederName);

                    if (_options.StopOnError)
                    {
                        _statusService.SetFailed($"Seeder {seeder.SeederName} failed: {ex.Message}");
                        throw;
                    }
                }
            }

            totalStopwatch.Stop();
            _statusService.SetCompleted(executedCount, failedCount, totalStopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "PerformanceTestDataSeeder: Orchestration complete - {Executed} seeders executed, {Failed} failed, total time: {ElapsedMs}ms",
                executedCount,
                failedCount,
                totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            totalStopwatch.Stop();
            _statusService.SetFailed(ex.Message);
            throw;
        }
    }
}
