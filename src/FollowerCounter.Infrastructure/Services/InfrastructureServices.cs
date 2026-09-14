using System.Text.Json;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Infrastructure.Persistence;
using FollowerCounter.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext dbContext, ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string result,
        Guid? userId = null,
        Guid? deviceId = null,
        string? ipAddress = null,
        string? correlationId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = action,
                Result = result,
                UserId = userId,
                DeviceId = deviceId,
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
                IpAddressHash = !string.IsNullOrWhiteSpace(ipAddress) ? CryptoHelper.ComputeSha256Hash(ipAddress) : null,
                MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log entry for action {Action}", action);
        }
    }
}

public class SecurityEventService : ISecurityEventService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SecurityEventService> _logger;

    public SecurityEventService(AppDbContext dbContext, ILogger<SecurityEventService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RecordEventAsync(
        string eventType,
        string severity,
        string? ipAddress = null,
        string? correlationId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var securityEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Severity = severity,
                Timestamp = DateTimeOffset.UtcNow,
                SourceIpHash = !string.IsNullOrWhiteSpace(ipAddress) ? CryptoHelper.ComputeSha256Hash(ipAddress) : null,
                CorrelationId = correlationId,
                DetailsJson = details != null ? JsonSerializer.Serialize(details) : null
            };

            _dbContext.SecurityEvents.Add(securityEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist security event {EventType}", eventType);
        }
    }
}
