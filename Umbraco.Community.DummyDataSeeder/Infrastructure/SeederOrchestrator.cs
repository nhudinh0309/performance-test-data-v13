namespace Umbraco.Community.DummyDataSeeder.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.DummyDataSeeder.Configuration;

/// <summary>
/// Hosted service that orchestrates the execution of all seeders in the correct order.
/// Registered as an IHostedService to run during application startup.
/// </summary>
public class SeederOrchestrator : IHostedService
{
    private readonly IEnumerable<ISeeder> _seeders;
    private readonly ILogger<SeederOrchestrator> _logger;
    private readonly SeederOptions _options;
    private readonly SeederConfigurationValidator _validator;
    private readonly SeederConfiguration _config;

    /// <summary>
    /// Creates a new SeederOrchestrator instance.
    /// </summary>
    public SeederOrchestrator(
        IEnumerable<ISeeder> seeders,
        ILogger<SeederOrchestrator> logger,
        IOptions<SeederOptions> options,
        IOptions<SeederConfiguration> config,
        SeederConfigurationValidator validator)
    {
        // Sort seeders by execution order
        _seeders = seeders.OrderBy(s => s.ExecutionOrder).ToList();
        _logger = logger;
        _options = options.Value;
        _config = config.Value;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DummyDataSeeder: Disabled in configuration, skipping all seeders");
            return;
        }

        // Validate configuration
        var validationResult = _validator.Validate(_config);
        if (!validationResult.IsValid)
        {
            _logger.LogError("DummyDataSeeder: Configuration validation failed:");
            foreach (var error in validationResult.Errors)
            {
                _logger.LogError("  - {Error}", error);
            }

            if (_options.StopOnError)
            {
                throw new InvalidOperationException(
                    $"DummyDataSeeder configuration is invalid: {string.Join(", ", validationResult.Errors)}");
            }

            _logger.LogWarning("DummyDataSeeder: Continuing despite validation errors (StopOnError=false)");
        }

        var seederList = _seeders.ToList();
        _logger.LogInformation(
            "DummyDataSeeder: Starting orchestration of {Count} seeders (FakerSeed: {Seed})",
            seederList.Count,
            _options.FakerSeed?.ToString() ?? "auto");

        var totalStopwatch = Stopwatch.StartNew();
        var executedCount = 0;
        var failedCount = 0;

        foreach (var seeder in seederList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("DummyDataSeeder: Orchestration cancelled");
                break;
            }

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
                _logger.LogError(ex, "DummyDataSeeder: Seeder {SeederName} failed", seeder.SeederName);

                if (_options.StopOnError)
                {
                    throw;
                }
            }
        }

        totalStopwatch.Stop();

        _logger.LogInformation(
            "DummyDataSeeder: Orchestration complete - {Executed} seeders executed, {Failed} failed, total time: {ElapsedMs}ms",
            executedCount,
            failedCount,
            totalStopwatch.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
