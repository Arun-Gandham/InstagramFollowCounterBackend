namespace FollowerCounter.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string result,
        Guid? userId = null,
        Guid? deviceId = null,
        string? ipAddress = null,
        string? correlationId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
