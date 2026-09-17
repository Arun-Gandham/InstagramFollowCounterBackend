using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Domain.Exceptions;
using FollowerCounter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FollowerCounter.Infrastructure.Workers;

public class InstagramFollowerRefreshWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<InstagramFollowerRefreshWorker> _logger;
    private readonly string _workerId = $"worker-{Guid.NewGuid():N}";

    public InstagramFollowerRefreshWorker(
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> options,
        ILogger<InstagramFollowerRefreshWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableFollowerRefresh)
        {
            _logger.LogInformation("InstagramFollowerRefreshWorker is disabled via configuration.");
            return;
        }

        _logger.LogInformation("InstagramFollowerRefreshWorker [{WorkerId}] started.", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessFollowerRefreshBatchAsync(stoppingToken);

                // Configurable delay with jitter (e.g., 30s ± 3s)
                var jitterSeconds = Random.Shared.Next(-3, 4);
                var delaySeconds = Math.Max(10, _options.FollowerRefreshIntervalSeconds + jitterSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in follower refresh worker loop.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("InstagramFollowerRefreshWorker [{WorkerId}] stopped.", _workerId);
    }

    private async Task ProcessFollowerRefreshBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instagramProvider = scope.ServiceProvider.GetRequiredService<IInstagramProvider>();
        var secretProtector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var now = DateTimeOffset.UtcNow;
        var leaseUntil = now.AddMinutes(2);

        // 1. Claim a batch of active accounts using DB lease locking
        var candidates = await dbContext.InstagramAccounts
            .Where(a => a.ConnectionStatus == InstagramConnectionStatus.Connected
                     || a.ConnectionStatus == InstagramConnectionStatus.TemporarilyUnavailable)
            .Where(a => !a.RequiresReauthorization && !string.IsNullOrEmpty(a.TokenEncrypted))
            .Where(a => a.LockedUntil == null || a.LockedUntil < now)
            .Where(a => a.NextRefreshAttemptAt == null || a.NextRefreshAttemptAt <= now)
            .OrderBy(a => a.LastFollowerRefreshAt ?? DateTimeOffset.MinValue)
            .Take(_options.FollowerRefreshBatchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        // Lock candidates for this worker instance
        foreach (var candidate in candidates)
        {
            candidate.LockedUntil = leaseUntil;
            candidate.LockedBy = _workerId;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Process concurrently with bounded parallel workers
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _options.FollowerRefreshMaxConcurrency),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(candidates, parallelOptions, async (account, ct) =>
        {
            // Per-account randomized micro-jitter (10-300ms) to spread HTTP requests smoothly
            await Task.Delay(Random.Shared.Next(10, 300), ct);

            using var accountScope = _serviceProvider.CreateScope();
            var perAccountDb = accountScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var perAccountProvider = accountScope.ServiceProvider.GetRequiredService<IInstagramProvider>();
            var perAccountProtector = accountScope.ServiceProvider.GetRequiredService<ISecretProtector>();

            var localAccount = await perAccountDb.InstagramAccounts
                .FirstOrDefaultAsync(a => a.Id == account.Id, ct);

            if (localAccount == null) return;

            var refreshTime = DateTimeOffset.UtcNow;

            try
            {
                var decryptedToken = perAccountProtector.Decrypt(localAccount.TokenEncrypted);
                var followerCount = await perAccountProvider.GetFollowerCountAsync(decryptedToken, localAccount.InstagramUserId, ct);

                var changed = localAccount.TryUpdateFollowers(followerCount, refreshTime);
                if (changed)
                {
                    var history = new FollowerHistory
                    {
                        Id = Guid.NewGuid(),
                        InstagramAccountId = localAccount.Id,
                        OldCount = localAccount.PreviousFollowerCount ?? followerCount,
                        NewCount = followerCount,
                        ChangedAt = refreshTime
                    };
                    perAccountDb.FollowerHistories.Add(history);
                    _logger.LogInformation("Followers updated for {Username}: {OldCount} -> {NewCount} (Sequence: {Seq})",
                        localAccount.Username, localAccount.PreviousFollowerCount, followerCount, localAccount.FollowerSequence);
                }

                localAccount.NextRefreshAttemptAt = null;
                localAccount.LockedUntil = null;
                localAccount.LockedBy = null;
                await perAccountDb.SaveChangesAsync(ct);
            }
            catch (DomainException ex) when (ex.Message.Contains("INSTAGRAM_REAUTH_REQUIRED"))
            {
                _logger.LogWarning("Account {Username} requires re-authorization. Error: {Error}", localAccount.Username, ex.Message);
                localAccount.MarkReauthorizationRequired("190_TOKEN_INVALID", refreshTime);
                localAccount.LockedUntil = null;
                localAccount.LockedBy = null;
                await perAccountDb.SaveChangesAsync(ct);
            }
            catch (RateLimitException rle)
            {
                _logger.LogWarning("Meta rate limit reached for {Username}. Backing off for {Minutes} min.", localAccount.Username, rle.RetryAfter.TotalMinutes);
                localAccount.MarkRateLimited(refreshTime, rle.RetryAfter);
                localAccount.LockedUntil = null;
                localAccount.LockedBy = null;
                await perAccountDb.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Temporary error querying followers for {Username}.", localAccount.Username);
                localAccount.MarkTemporarilyUnavailable("API_TRANSIENT_FAILURE", refreshTime, TimeSpan.FromMinutes(2));
                localAccount.LockedUntil = null;
                localAccount.LockedBy = null;
                await perAccountDb.SaveChangesAsync(ct);
            }
        });
    }
}
