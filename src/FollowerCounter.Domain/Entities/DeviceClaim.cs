namespace FollowerCounter.Domain.Entities;

public class DeviceClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public string ClaimTokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public Guid? UsedByUserId { get; set; }
    public AppUser? UsedByUser { get; set; }

    public bool IsValid(DateTimeOffset now)
    {
        return UsedAt == null && ExpiresAt > now;
    }

    public void MarkUsed(Guid userId, DateTimeOffset now)
    {
        UsedAt = now;
        UsedByUserId = userId;
    }
}
