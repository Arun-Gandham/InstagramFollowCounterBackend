using FollowerCounter.Application.Common.Interfaces;
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

public class InstagramTokenRefreshWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<InstagramTokenRefreshWorker> _logger;

    public InstagramTokenRefreshWorker(
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> options,
        ILogger<InstagramTokenRefreshWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableTokenRefresh)
        {
            _logger.LogInformation("InstagramTokenRefreshWorker is disabled via configuration.");
            return;
        }

        _logger.LogInformation("InstagramTokenRefreshWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during token refresh job execution.");
            }

            var delayHours = Math.Max(1, _options.TokenRefreshCheckIntervalHours);
            await Task.Delay(TimeSpan.FromHours(delayHours), stoppingToken);
        }

        _logger.LogInformation("InstagramTokenRefreshWorker stopped.");
    }

    private async Task RefreshExpiringTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instagramProvider = scope.ServiceProvider.GetRequiredService<IInstagramProvider>();
        var secretProtector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var now = DateTimeOffset.UtcNow;
        var thresholdDate = now.AddDays(_options.TokenRefreshDaysThreshold);

        // Find connected accounts with long-lived tokens expiring within threshold
        var expiringAccounts = await dbContext.InstagramAccounts
            .Where(a => a.ConnectionStatus == InstagramConnectionStatus.Connected)
            .Where(a => !a.RequiresReauthorization && !string.IsNullOrEmpty(a.TokenEncrypted))
            .Where(a => a.TokenExpiresAt != null && a.TokenExpiresAt <= thresholdDate)
            .OrderBy(a => a.TokenExpiresAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (expiringAccounts.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} Instagram account(s) due for 60-day token refresh.", expiringAccounts.Count);

        foreach (var account in expiringAccounts)
        {
            try
            {
                var currentToken = secretProtector.Decrypt(account.TokenEncrypted);
                var newResult = await instagramProvider.RefreshAccessTokenAsync(currentToken, cancellationToken);

                if (newResult != null && !string.IsNullOrWhiteSpace(newResult.AccessToken))
                {
                    // 1. Encrypt new token
                    var encryptedNewToken = secretProtector.Encrypt(newResult.AccessToken);
                    var newExpiresAt = newResult.ExpiresAt ?? now.AddDays(60);

                    // 2. Transactionally update DB
                    account.RecordTokenRefreshSuccess(encryptedNewToken, newExpiresAt, now);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Successfully refreshed long-lived token for {Username}. New expiry: {ExpiresAt}",
                        account.Username, newExpiresAt);
                }
            }
            catch (DomainException dex) when (dex.Message.Contains("INSTAGRAM_REAUTH_REQUIRED"))
            {
                _logger.LogWarning("Token refresh failed permanently for {Username}: {Message}", account.Username, dex.Message);
                account.RecordTokenRefreshFailure("190_REFRESH_REVOKED", now, isPermanent: true);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transient failure during token refresh for {Username}.", account.Username);
                account.RecordTokenRefreshFailure("REFRESH_TRANSIENT_FAILURE", now, isPermanent: false, retryDelay: TimeSpan.FromHours(2));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
