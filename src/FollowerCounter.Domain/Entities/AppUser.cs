using FollowerCounter.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace FollowerCounter.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<InstagramAccount> InstagramAccounts { get; set; } = new List<InstagramAccount>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<InstagramOAuthSession> OAuthSessions { get; set; } = new List<InstagramOAuthSession>();
}
