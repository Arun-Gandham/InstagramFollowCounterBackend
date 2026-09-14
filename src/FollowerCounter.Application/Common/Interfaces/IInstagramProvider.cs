using FollowerCounter.Application.DTOs.Instagram;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IInstagramProvider
{
    Task<string> BuildAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default);

    Task<InstagramTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<InstagramTokenResult?> RefreshAccessTokenAsync(string currentAccessToken, CancellationToken cancellationToken = default);

    Task<InstagramProfile> GetCurrentProfileAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<long> GetFollowerCountAsync(string accessToken, string instagramUserId, CancellationToken cancellationToken = default);

    Task RevokeAuthorizationAsync(string accessToken, CancellationToken cancellationToken = default);
}
