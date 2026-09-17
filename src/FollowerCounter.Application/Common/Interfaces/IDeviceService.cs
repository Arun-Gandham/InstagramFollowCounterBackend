using FollowerCounter.Application.DTOs.Device;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IDeviceService
{
    Task<ClaimDeviceResponseDto> ClaimDeviceAsync(Guid userId, ClaimDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceDto>> GetUserDevicesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task BindInstagramAccountAsync(Guid userId, Guid deviceId, Guid instagramAccountId, string? ipAddress, CancellationToken cancellationToken = default);

    Task UnbindInstagramAccountAsync(Guid userId, Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<DeviceDto> UpdateDeviceNicknameAsync(Guid userId, Guid deviceId, string? nickname, CancellationToken cancellationToken = default);
}
