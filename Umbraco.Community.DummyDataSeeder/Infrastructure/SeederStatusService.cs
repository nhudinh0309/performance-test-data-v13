namespace Umbraco.Community.DummyDataSeeder.Infrastructure;

/// <summary>
/// Tracks the status of the seeding operation.
/// </summary>
public class SeederStatusService
{
    /// <summary>
    /// Current status of the seeding operation.
    /// </summary>
    public SeederStatus Status { get; private set; } = SeederStatus.NotStarted;

    /// <summary>
    /// Error message if seeding failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Total elapsed time in milliseconds.
    /// </summary>
    public long ElapsedMs { get; private set; }

    /// <summary>
    /// Number of seeders that executed successfully.
    /// </summary>
    public int ExecutedCount { get; private set; }

    /// <summary>
    /// Number of seeders that failed.
    /// </summary>
    public int FailedCount { get; private set; }

    /// <summary>
    /// Name of the currently running seeder.
    /// </summary>
    public string? CurrentSeeder { get; private set; }

    /// <summary>
    /// Marks seeding as started.
    /// </summary>
    public void SetStarted()
    {
        Status = SeederStatus.Running;
        ErrorMessage = null;
    }

    /// <summary>
    /// Updates the currently running seeder.
    /// </summary>
    public void SetCurrentSeeder(string seederName)
    {
        CurrentSeeder = seederName;
    }

    /// <summary>
    /// Marks seeding as skipped (disabled in configuration).
    /// </summary>
    public void SetSkipped()
    {
        Status = SeederStatus.Skipped;
    }

    /// <summary>
    /// Marks seeding as completed successfully.
    /// </summary>
    public void SetCompleted(int executedCount, int failedCount, long elapsedMs)
    {
        Status = failedCount > 0 ? SeederStatus.CompletedWithErrors : SeederStatus.Completed;
        ExecutedCount = executedCount;
        FailedCount = failedCount;
        ElapsedMs = elapsedMs;
        CurrentSeeder = null;
    }

    /// <summary>
    /// Marks seeding as failed.
    /// </summary>
    public void SetFailed(string errorMessage)
    {
        Status = SeederStatus.Failed;
        ErrorMessage = errorMessage;
        CurrentSeeder = null;
    }
}

/// <summary>
/// Possible states of the seeding operation.
/// </summary>
public enum SeederStatus
{
    /// <summary>
    /// Seeding has not started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Seeding is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Seeding completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Seeding completed but some seeders failed.
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// Seeding failed completely.
    /// </summary>
    Failed,

    /// <summary>
    /// Seeding was skipped (disabled in configuration).
    /// </summary>
    Skipped
}
