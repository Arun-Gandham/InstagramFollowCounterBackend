using FollowerCounter.Application.DTOs.Admin;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IAdminService
{
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminDeviceDto>> GetDevicesAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<CreateDeviceResponseDto> CreateDeviceAsync(CreateDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default);

    Task DisableDeviceAsync(Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<string> ResetClaimAsync(Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminInstagramConnectionDto>> GetInstagramConnectionsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<AdminSystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default);
}
