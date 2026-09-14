namespace FollowerCounter.Application.Common.Interfaces;

public interface ISecurityEventService
{
    Task RecordEventAsync(
        string eventType,
        string severity,
        string? ipAddress = null,
        string? correlationId = null,
        object? details = null,
        CancellationToken cancellationToken = default);
}
