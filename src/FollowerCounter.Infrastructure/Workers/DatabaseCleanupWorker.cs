using FollowerCounter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FollowerCounter.Infrastructure.Workers;

public class DatabaseCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<DatabaseCleanupWorker> _logger;

    public DatabaseCleanupWorker(
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> options,
        ILogger<DatabaseCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableCleanup)
        {
            _logger.LogInformation("DatabaseCleanupWorker is disabled via configuration.");
            return;
        }

        _logger.LogInformation("DatabaseCleanupWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
                var delayHours = Math.Max(1, _options.CleanupIntervalHours);
                await Task.Delay(TimeSpan.FromHours(delayHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database cleanup job execution.");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("DatabaseCleanupWorker stopped.");
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var expiredThreshold = now.AddDays(-1); // Delete sessions expired more than 24h ago

        // 1. Delete expired / used OAuth sessions
        var deletedOAuthSessions = await dbContext.InstagramOAuthSessions
            .Where(s => s.ExpiresAt < expiredThreshold || (s.UsedAt != null && s.UsedAt < expiredThreshold))
            .ExecuteDeleteAsync(cancellationToken);

        // 2. Delete expired unused claims older than 90 days
        var claimThreshold = now.AddDays(-90);
        var deletedClaims = await dbContext.DeviceClaims
            .Where(c => c.ExpiresAt < claimThreshold && c.UsedAt == null)
            .ExecuteDeleteAsync(cancellationToken);

        // 3. Prune old security events older than 60 days
        var securityEventThreshold = now.AddDays(-60);
        var deletedEvents = await dbContext.SecurityEvents
            .Where(e => e.Timestamp < securityEventThreshold)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Cleanup complete: removed {OAuthCount} OAuth sessions, {ClaimsCount} expired claims, {EventsCount} security events. (AuditLogs strictly preserved).",
            deletedOAuthSessions, deletedClaims, deletedEvents);
    }
}
