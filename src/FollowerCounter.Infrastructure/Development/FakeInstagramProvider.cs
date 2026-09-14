using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Instagram;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Exceptions;

namespace FollowerCounter.Infrastructure.Development;

public class FakeInstagramProvider : IInstagramProvider
{
    private static long _currentFollowerCount = 18920;
    private static bool _simulateOutage = false;
    private static bool _simulateRateLimit = false;
    private static bool _simulateRevocation = false;

    public static long CurrentFollowerCount => _currentFollowerCount;

    public static void SetFollowerCount(long count)
    {
        _currentFollowerCount = count;
    }

    public static void IncrementFollowerCount(long delta = 1)
    {
        Interlocked.Add(ref _currentFollowerCount, delta);
    }

    public static void SetSimulateOutage(bool outage) => _simulateOutage = outage;
    public static void SetSimulateRateLimit(bool rateLimit) => _simulateRateLimit = rateLimit;
    public static void SetSimulateRevocation(bool revoke) => _simulateRevocation = revoke;

    public Task<string> BuildAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default)
    {
        // Redirects directly to local callback with dummy authorization code
        var mockUrl = $"/api/v1/instagram/callback?code=mock_auth_code_12345&state={Uri.EscapeDataString(state)}";
        return Task.FromResult(mockUrl);
    }

    public Task<InstagramTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        CheckSimulatedFailures();

        if (code == "rejected")
        {
            throw new DomainException("User rejected Instagram authorization.");
        }

        var result = new InstagramTokenResult(
            AccessToken: $"fake_ig_access_token_{Guid.NewGuid():N}",
            TokenType: "bearer",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(60),
            InstagramUserId: "17841400000000099",
            Scopes: "instagram_business_basic"
        );

        return Task.FromResult(result);
    }

    public Task<InstagramTokenResult?> RefreshAccessTokenAsync(string currentAccessToken, CancellationToken cancellationToken = default)
    {
        CheckSimulatedFailures();

        var result = new InstagramTokenResult(
            AccessToken: $"fake_ig_refreshed_token_{Guid.NewGuid():N}",
            TokenType: "bearer",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(60),
            InstagramUserId: "17841400000000099",
            Scopes: "instagram_business_basic"
        );

        return Task.FromResult<InstagramTokenResult?>(result);
    }

    public Task<InstagramProfile> GetCurrentProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        CheckSimulatedFailures();

        var profile = new InstagramProfile(
            Id: "17841400000000099",
            Username: "demo_creator",
            Name: "Demo Creator Studio",
            AccountType: "CREATOR",
            FollowersCount: _currentFollowerCount
        );

        return Task.FromResult(profile);
    }

    public Task<long> GetFollowerCountAsync(string accessToken, string instagramUserId, CancellationToken cancellationToken = default)
    {
        CheckSimulatedFailures();
        return Task.FromResult(_currentFollowerCount);
    }

    public Task RevokeAuthorizationAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static void CheckSimulatedFailures()
    {
        if (_simulateOutage)
        {
            throw new HttpRequestException("Simulated Meta Graph API 503 Outage");
        }

        if (_simulateRateLimit)
        {
            throw new RateLimitException("Simulated Meta 429 Rate Limit", TimeSpan.FromMinutes(5));
        }

        if (_simulateRevocation)
        {
            throw new DomainException("INSTAGRAM_REAUTH_REQUIRED: Simulated revoked token (code 190)");
        }
    }
}
