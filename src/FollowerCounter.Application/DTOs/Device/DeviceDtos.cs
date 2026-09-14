using FollowerCounter.Domain.Enums;

namespace FollowerCounter.Application.DTOs.Device;

public record ClaimDeviceRequestDto(
    string SerialNumber,
    string ClaimCode
);

public record ClaimDeviceResponseDto(
    Guid DeviceId,
    string SerialNumber,
    DateTimeOffset ClaimedAt
);

public record DeviceLinkedInstagramDto(
    Guid Id,
    string Username,
    long? FollowerCount,
    InstagramConnectionStatus ConnectionStatus
);

public record DeviceDto(
    Guid Id,
    string SerialNumber,
    DeviceStatus Status,
    string? FirmwareVersion,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ClaimedAt,
    DateTimeOffset CreatedAt,
    DeviceLinkedInstagramDto? LinkedInstagramAccount
);

public record DeviceStateResponseDto(
    string DeviceId,
    bool Configured,
    bool InstagramConnected,
    string? Username,
    long Followers,
    long Sequence,
    bool IsStale,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset ServerTime,
    int PollAfterSeconds
);

public record DeviceHeartbeatRequestDto(
    string? FirmwareVersion,
    long? UptimeSeconds,
    int? WifiRssi,
    long? FreeHeap,
    long? LastSequence,
    string? Status
);

public record DeviceHeartbeatResponseDto(
    bool Acknowledged,
    DateTimeOffset ServerTime
);
