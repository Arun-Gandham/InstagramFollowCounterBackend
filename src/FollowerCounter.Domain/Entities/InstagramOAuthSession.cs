namespace FollowerCounter.Domain.Entities;

public class InstagramOAuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string StateHash { get; set; } = string.Empty;
    public string? PkceVerifierEncrypted { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public string? RedirectAfterSuccess { get; set; }
    public string? IpHash { get; set; }

    public bool IsValid(DateTimeOffset now)
    {
        return UsedAt == null && ExpiresAt > now;
    }

    public void MarkUsed(DateTimeOffset now)
    {
        UsedAt = now;
    }
}
