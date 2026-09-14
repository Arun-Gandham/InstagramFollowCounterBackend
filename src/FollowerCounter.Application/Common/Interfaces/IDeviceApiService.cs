using FollowerCounter.Application.DTOs.Device;
using FollowerCounter.Domain.Entities;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IDeviceApiService
{
    Task<Device?> AuthenticateDeviceAsync(string serialNumber, string plaintextSecret, CancellationToken cancellationToken = default);

    Task<DeviceStateResponseDto> GetDeviceStateAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<DeviceHeartbeatResponseDto> RecordHeartbeatAsync(Guid deviceId, DeviceHeartbeatRequestDto request, CancellationToken cancellationToken = default);
}
