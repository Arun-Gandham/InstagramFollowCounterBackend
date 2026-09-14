using FollowerCounter.Domain.Enums;

namespace FollowerCounter.Application.DTOs.Instagram;

public record InstagramConnectResponseDto(
    string AuthorizationUrl
);

public record InstagramAccountDto(
    Guid Id,
    string InstagramUserId,
    string Username,
    string? AccountType,
    InstagramConnectionStatus ConnectionStatus,
    bool RequiresReauthorization,
    long? FollowerCount,
    long? PreviousFollowerCount,
    long FollowerSequence,
    DateTimeOffset? LastFollowerRefreshAt,
    DateTimeOffset? TokenExpiresAt,
    DateTimeOffset CreatedAt
);

public record RefreshFollowerResultDto(
    long FollowerCount,
    long FollowerSequence,
    bool Changed,
    DateTimeOffset LastFollowerRefreshAt
);

public record InstagramConnectionStatusDto(
    string ConnectionStatus,
    bool RequiresAction,
    DateTimeOffset? LastSuccessfulApiCallAt,
    DateTimeOffset? TokenExpiresAt
);
