namespace FollowerCounter.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid? DeviceId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; set; } = string.Empty;
    public string? IpAddressHash { get; set; }
    public string? MetadataJson { get; set; }
}
