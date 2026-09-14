using FollowerCounter.Domain.Enums;

namespace FollowerCounter.Application.DTOs.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    int DeviceCount,
    int InstagramAccountCount
);

public record AdminDeviceDto(
    Guid Id,
    string SerialNumber,
    DeviceStatus Status,
    string? FirmwareVersion,
    Guid? OwnerUserId,
    string? OwnerEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClaimedAt,
    DateTimeOffset? LastSeenAt
);

public record CreateDeviceRequestDto(
    string SerialNumber
);

public record CreateDeviceResponseDto(
    Guid DeviceId,
    string SerialNumber,
    string PlaintextDeviceSecret,
    string PlaintextClaimCode,
    DateTimeOffset ClaimExpiresAt
);

public record AdminInstagramConnectionDto(
    Guid Id,
    string InstagramUserId,
    string Username,
    string? AccountType,
    Guid OwnerUserId,
    string OwnerEmail,
    InstagramConnectionStatus ConnectionStatus,
    bool RequiresReauthorization,
    long? FollowerCount,
    long FollowerSequence,
    DateTimeOffset? LastFollowerRefreshAt,
    DateTimeOffset? NextRefreshAttemptAt,
    int TokenRefreshFailureCount,
    string? LastApiErrorCode,
    DateTimeOffset? LastApiErrorAt
);

public record AdminSystemHealthDto(
    string Status,
    int TotalUsers,
    int TotalActiveDevices,
    int TotalConnectedInstagramAccounts,
    int AccountsRequiringReauth,
    int RateLimitedAccounts,
    DateTimeOffset ServerTime
);
