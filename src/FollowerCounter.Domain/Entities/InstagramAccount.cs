using FollowerCounter.Domain.Enums;

namespace FollowerCounter.Domain.Entities;

public class InstagramAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public AppUser OwnerUser { get; set; } = null!;

    public string InstagramUserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AccountType { get; set; }

    public InstagramConnectionStatus ConnectionStatus { get; set; } = InstagramConnectionStatus.Connected;

    public string TokenEncrypted { get; set; } = string.Empty;
    public string? RefreshTokenEncrypted { get; set; }
    public string TokenType { get; set; } = "bearer";

    public DateTimeOffset TokenIssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? TokenExpiresAt { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
    public string Scopes { get; set; } = string.Empty;

    public long? FollowerCount { get; set; }
    public long? PreviousFollowerCount { get; set; }
    public long FollowerSequence { get; set; } = 0;

    public DateTimeOffset? LastFollowerRefreshAt { get; set; }
    public DateTimeOffset? LastSuccessfulApiCallAt { get; set; }
    public DateTimeOffset? LastTokenRefreshAt { get; set; }
    public DateTimeOffset? NextRefreshAttemptAt { get; set; }
    public int TokenRefreshFailureCount { get; set; } = 0;

    public string? LastApiErrorCode { get; set; }
    public DateTimeOffset? LastApiErrorAt { get; set; }
    public bool RequiresReauthorization { get; set; } = false;

    // Concurrency leasing for background workers
    public DateTimeOffset? LockedUntil { get; set; }
    public string? LockedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DeviceInstagramBinding> DeviceBindings { get; set; } = new List<DeviceInstagramBinding>();
    public ICollection<FollowerHistory> FollowerHistories { get; set; } = new List<FollowerHistory>();

    /// <summary>
    /// Atomically updates follower count according to Domain rules.
    /// If count changed, sets previous count, current count, increments sequence, and returns true.
    /// If unchanged, preserves sequence and returns false.
    /// </summary>
    public bool TryUpdateFollowers(long newCount, DateTimeOffset refreshTime)
    {
        LastFollowerRefreshAt = refreshTime;
        LastSuccessfulApiCallAt = refreshTime;
        ConnectionStatus = InstagramConnectionStatus.Connected;
        RequiresReauthorization = false;
        LastApiErrorCode = null;
        LastApiErrorAt = null;
        UpdatedAt = refreshTime;

        if (FollowerCount == newCount)
        {
            return false;
        }

        PreviousFollowerCount = FollowerCount ?? newCount;
        FollowerCount = newCount;
        FollowerSequence++;
        return true;
    }

    public void MarkReauthorizationRequired(string errorCode, DateTimeOffset timestamp)
    {
        ConnectionStatus = InstagramConnectionStatus.ReauthorizationRequired;
        RequiresReauthorization = true;
        LastApiErrorCode = errorCode;
        LastApiErrorAt = timestamp;
        UpdatedAt = timestamp;
        // NOTE: Never reset or delete FollowerCount!
    }

    public void MarkTemporarilyUnavailable(string errorCode, DateTimeOffset timestamp, TimeSpan retryDelay)
    {
        ConnectionStatus = InstagramConnectionStatus.TemporarilyUnavailable;
        LastApiErrorCode = errorCode;
        LastApiErrorAt = timestamp;
        NextRefreshAttemptAt = timestamp.Add(retryDelay);
        UpdatedAt = timestamp;
    }

    public void MarkRateLimited(DateTimeOffset timestamp, TimeSpan backoff)
    {
        ConnectionStatus = InstagramConnectionStatus.RateLimited;
        LastApiErrorCode = "429_RATE_LIMITED";
        LastApiErrorAt = timestamp;
        NextRefreshAttemptAt = timestamp.Add(backoff);
        UpdatedAt = timestamp;
    }

    public void RecordTokenRefreshSuccess(string encryptedNewToken, DateTimeOffset newExpiresAt, DateTimeOffset timestamp)
    {
        TokenEncrypted = encryptedNewToken;
        TokenExpiresAt = newExpiresAt;
        LastTokenRefreshAt = timestamp;
        TokenRefreshFailureCount = 0;
        NextRefreshAttemptAt = null;
        ConnectionStatus = InstagramConnectionStatus.Connected;
        RequiresReauthorization = false;
        LastApiErrorCode = null;
        LastApiErrorAt = null;
        UpdatedAt = timestamp;
    }

    public void RecordTokenRefreshFailure(string errorCode, DateTimeOffset timestamp, bool isPermanent, TimeSpan? retryDelay = null)
    {
        TokenRefreshFailureCount++;
        LastApiErrorCode = errorCode;
        LastApiErrorAt = timestamp;
        UpdatedAt = timestamp;

        if (isPermanent)
        {
            MarkReauthorizationRequired(errorCode, timestamp);
        }
        else if (retryDelay.HasValue)
        {
            NextRefreshAttemptAt = timestamp.Add(retryDelay.Value);
        }
    }
}
