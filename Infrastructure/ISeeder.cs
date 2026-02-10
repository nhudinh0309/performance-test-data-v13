namespace Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

/// <summary>
/// Interface for all seeders in the PerformanceTestDataSeeder package.
/// </summary>
public interface ISeeder
{
    /// <summary>
    /// The order in which this seeder should execute (1-7).
    /// Lower numbers execute first.
    /// </summary>
    int ExecutionOrder { get; }

    /// <summary>
    /// Display name for logging purposes.
    /// </summary>
    string SeederName { get; }

    /// <summary>
    /// Execute the seeder.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
