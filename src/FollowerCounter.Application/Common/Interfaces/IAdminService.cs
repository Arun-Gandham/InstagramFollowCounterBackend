using FollowerCounter.Application.DTOs.Admin;
using FollowerCounter.Application.DTOs.Common;
using FollowerCounter.Domain.Enums;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IAdminService
{
    Task<PagedResultDto<AdminUserDto>> GetUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminDeviceDto>> GetDevicesAsync(int page = 1, int pageSize = 50, string? search = null, DeviceStatus? status = null, CancellationToken cancellationToken = default);

    Task<CreateDeviceResponseDto> CreateDeviceAsync(CreateDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AdminDeviceDto> UpdateDeviceAsync(Guid deviceId, UpdateDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task DisableDeviceAsync(Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<string> ResetClaimAsync(Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminInstagramConnectionDto>> GetInstagramConnectionsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<AdminSystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default);
}
