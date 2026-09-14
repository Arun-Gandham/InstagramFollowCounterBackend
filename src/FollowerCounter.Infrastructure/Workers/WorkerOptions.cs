namespace FollowerCounter.Infrastructure.Workers;

public class WorkerOptions
{
    public const string SectionName = "Workers";

    public bool EnableFollowerRefresh { get; set; } = true;
    public int FollowerRefreshIntervalSeconds { get; set; } = 30;
    public int FollowerRefreshBatchSize { get; set; } = 20;
    public int FollowerRefreshMaxConcurrency { get; set; } = 5;

    public bool EnableTokenRefresh { get; set; } = true;
    public int TokenRefreshCheckIntervalHours { get; set; } = 6;
    public int TokenRefreshDaysThreshold { get; set; } = 7;

    public bool EnableCleanup { get; set; } = true;
    public int CleanupIntervalHours { get; set; } = 12;
}
