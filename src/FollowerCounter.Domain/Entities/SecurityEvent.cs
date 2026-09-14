namespace FollowerCounter.Domain.Entities;

public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? SourceIpHash { get; set; }
    public string? CorrelationId { get; set; }
    public string? DetailsJson { get; set; }
}
