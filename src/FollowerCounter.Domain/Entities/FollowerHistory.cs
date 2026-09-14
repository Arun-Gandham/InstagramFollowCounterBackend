namespace FollowerCounter.Domain.Entities;

public class FollowerHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstagramAccountId { get; set; }
    public InstagramAccount InstagramAccount { get; set; } = null!;

    public long OldCount { get; set; }
    public long NewCount { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
