using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Instagram;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FollowerCounter.Infrastructure.Meta;

public class MetaInstagramProvider : IInstagramProvider
{
    private readonly HttpClient _httpClient;
    private readonly MetaOptions _options;
    private readonly ILogger<MetaInstagramProvider> _logger;

    public MetaInstagramProvider(
        HttpClient httpClient,
        IOptions<MetaOptions> options,
        ILogger<MetaInstagramProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> BuildAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default)
    {
        var authUrl = $"{_options.AuthorizationUrl}" +
                      $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
                      $"&response_type=code" +
                      $"&scope=instagram_business_basic" +
                      $"&state={Uri.EscapeDataString(state)}" +
                      $"&force_authentication=1" +
                      $"&enable_fb_login=0";

        return Task.FromResult(authUrl);
    }

    public async Task<InstagramTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var cleanCode = code.EndsWith("#_") ? code[..^2] : code;

        var tokenForm = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = cleanCode
        };

        _logger.LogInformation("Exchanging OAuth authorization code with Meta for short-lived access token.");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(tokenForm)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta token exchange failed with HTTP {StatusCode}", response.StatusCode);
            HandleMetaErrorResponse(response.StatusCode, content);
        }

        var shortLived = JsonSerializer.Deserialize<MetaShortLivedTokenResponse>(content);
        if (shortLived == null || string.IsNullOrWhiteSpace(shortLived.AccessToken))
        {
            throw new DomainException("Meta returned an invalid or empty access token response.");
        }

        // Attempt to exchange short-lived token for long-lived token (60-day validity)
        string finalToken = shortLived.AccessToken;
        long expiresIn = 3600; // Default short-lived token validity is 1 hour
        string tokenType = "bearer";

        try
        {
            var longLived = await ExchangeForLongLivedTokenAsync(shortLived.AccessToken, cancellationToken);
            finalToken = longLived.AccessToken!;
            tokenType = longLived.TokenType ?? "bearer";
            expiresIn = longLived.ExpiresInSeconds > 0 ? longLived.ExpiresInSeconds : 60 * 24 * 3600; // fallback to 60 days
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to exchange short-lived token for long-lived token. Falling back to short-lived token.");
        }

        var userIdString = shortLived.UserId?.ToString() ?? string.Empty;
        var scopes = shortLived.Permissions != null ? string.Join(",", shortLived.Permissions) : "instagram_business_basic";

        return new InstagramTokenResult(
            AccessToken: finalToken,
            TokenType: tokenType,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            InstagramUserId: userIdString,
            Scopes: scopes
        );
    }

    private async Task<MetaLongLivedTokenResponse> ExchangeForLongLivedTokenAsync(string shortLivedToken, CancellationToken cancellationToken)
    {
        var exchangeUrl = $"{_options.GraphBaseUrl}/{_options.ApiVersion}/access_token" +
                          $"?grant_type=ig_exchange_token" +
                          $"&client_secret={Uri.EscapeDataString(_options.ClientSecret)}" +
                          $"&access_token={Uri.EscapeDataString(shortLivedToken)}";

        using var response = await _httpClient.GetAsync(exchangeUrl, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta long-lived token exchange failed with HTTP {StatusCode}", response.StatusCode);
            HandleMetaErrorResponse(response.StatusCode, content);
        }

        var longLived = JsonSerializer.Deserialize<MetaLongLivedTokenResponse>(content);
        if (longLived == null || string.IsNullOrWhiteSpace(longLived.AccessToken))
        {
            throw new DomainException("Meta returned an invalid long-lived access token payload.");
        }

        return longLived;
    }

    public async Task<InstagramTokenResult?> RefreshAccessTokenAsync(string currentAccessToken, CancellationToken cancellationToken = default)
    {
        var refreshUrl = $"{_options.GraphBaseUrl}/{_options.ApiVersion}/refresh_access_token" +
                         $"?grant_type=ig_refresh_token" +
                         $"&access_token={Uri.EscapeDataString(currentAccessToken)}";

        using var response = await _httpClient.GetAsync(refreshUrl, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta token refresh failed with HTTP {StatusCode}", response.StatusCode);
            HandleMetaErrorResponse(response.StatusCode, content);
        }

        var refreshed = JsonSerializer.Deserialize<MetaLongLivedTokenResponse>(content);
        if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
        {
            return null;
        }

        return new InstagramTokenResult(
            AccessToken: refreshed.AccessToken,
            TokenType: refreshed.TokenType ?? "bearer",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresInSeconds),
            InstagramUserId: null,
            Scopes: "instagram_business_basic"
        );
    }

    public async Task<InstagramProfile> GetCurrentProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var url = $"{_options.GraphBaseUrl}/{_options.ApiVersion}/me?fields=id,username,name,account_type,followers_count&access_token={Uri.EscapeDataString(accessToken)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta get profile failed with HTTP {StatusCode}", response.StatusCode);
            HandleMetaErrorResponse(response.StatusCode, content);
        }

        var profile = JsonSerializer.Deserialize<MetaProfileResponse>(content);
        if (profile == null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Username))
        {
            throw new DomainException("Meta returned an incomplete Instagram profile payload.");
        }

        return new InstagramProfile(
            Id: profile.Id,
            Username: profile.Username,
            Name: profile.Name,
            AccountType: profile.AccountType,
            FollowersCount: profile.FollowersCount
        );
    }

    public async Task<long> GetFollowerCountAsync(string accessToken, string instagramUserId, CancellationToken cancellationToken = default)
    {
        var targetNode = string.IsNullOrWhiteSpace(instagramUserId) ? "me" : instagramUserId;
        var url = $"{_options.GraphBaseUrl}/{_options.ApiVersion}/{targetNode}?fields=followers_count&access_token={Uri.EscapeDataString(accessToken)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Meta follower count lookup failed for {Node} with HTTP {StatusCode}", targetNode, response.StatusCode);
            HandleMetaErrorResponse(response.StatusCode, content);
        }

        var countResponse = JsonSerializer.Deserialize<MetaFollowerCountResponse>(content);
        if (countResponse == null || !countResponse.FollowersCount.HasValue)
        {
            throw new DomainException($"Follower count metric was not returned for Instagram account {targetNode}.");
        }

        return countResponse.FollowersCount.Value;
    }

    public async Task RevokeAuthorizationAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/me/permissions?access_token={Uri.EscapeDataString(accessToken)}";
            using var response = await _httpClient.DeleteAsync(url, cancellationToken);
            _logger.LogInformation("Revocation request sent to Meta with response {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to revoke Meta permissions cleanly. Proceeding with local token deletion.");
        }
    }

    private static void HandleMetaErrorResponse(HttpStatusCode statusCode, string responseContent)
    {
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RateLimitException("Meta API rate limit exceeded.", TimeSpan.FromMinutes(15));
        }

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var code = errorElement.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
                var subcode = errorElement.TryGetProperty("error_subcode", out var sc) ? sc.GetInt32() : 0;
                var message = errorElement.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown error" : "Unknown error";

                // Error Code 190: Invalid OAuth 2.0 Access Token
                if (code == 190 || subcode == 463 || subcode == 467)
                {
                    throw new DomainException($"INSTAGRAM_REAUTH_REQUIRED: {message} (code {code})");
                }

                // Error Code 4, 17, 32: Rate limiting
                if (code is 4 or 17 or 32)
                {
                    throw new RateLimitException($"Meta rate limit: {message}", TimeSpan.FromMinutes(10));
                }

                throw new DomainException($"META_API_ERROR: {message} (code {code})");
            }
        }
        catch (JsonException)
        {
            // fallback if response is not JSON
        }

        throw new DomainException($"Meta API request failed with HTTP {(int)statusCode} {statusCode}.");
    }

    private class MetaShortLivedTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("user_id")]
        public object? UserId { get; set; }

        [JsonPropertyName("permissions")]
        public List<string>? Permissions { get; set; }
    }

    private class MetaLongLivedTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public long ExpiresInSeconds { get; set; }
    }

    private class MetaProfileResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("account_type")]
        public string? AccountType { get; set; }

        [JsonPropertyName("followers_count")]
        public long? FollowersCount { get; set; }
    }

    private class MetaFollowerCountResponse
    {
        [JsonPropertyName("followers_count")]
        public long? FollowersCount { get; set; }
    }
}
