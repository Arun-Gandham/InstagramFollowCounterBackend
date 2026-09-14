namespace FollowerCounter.Application.DTOs.Instagram;

public record InstagramTokenResult(
    string AccessToken,
    string TokenType,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    string? InstagramUserId,
    string? Scopes,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null
);
