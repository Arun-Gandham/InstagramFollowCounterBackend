namespace FollowerCounter.Domain.Entities;

public class DeviceInstagramBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public Guid InstagramAccountId { get; set; }
    public InstagramAccount InstagramAccount { get; set; } = null!;

    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
