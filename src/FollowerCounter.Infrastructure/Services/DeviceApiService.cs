using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Device;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Infrastructure.Services;

public class DeviceApiService : IDeviceApiService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<DeviceApiService> _logger;

    public DeviceApiService(IAppDbContext dbContext, ILogger<DeviceApiService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Device?> AuthenticateDeviceAsync(string serialNumber, string plaintextSecret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(plaintextSecret))
        {
            return null;
        }

        var device = await _dbContext.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber.Trim(), cancellationToken);

        if (device == null)
        {
            return null;
        }

        if (device.Status == DeviceStatus.Disabled || device.Status == DeviceStatus.Revoked)
        {
            _logger.LogWarning("Authentication rejected for {Status} device {SerialNumber}", device.Status, serialNumber);
            return null;
        }

        var secretHash = CryptoHelper.ComputeSha256Hash(plaintextSecret.Trim());
        if (!CryptoHelper.FixedTimeEquals(device.CredentialHash, secretHash))
        {
            _logger.LogWarning("Invalid credential supplied for device {SerialNumber}", serialNumber);
            return null;
        }

        return device;
    }

    public async Task<DeviceStateResponseDto> GetDeviceStateAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.InstagramBindings)
                .ThenInclude(b => b.InstagramAccount)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        var now = DateTimeOffset.UtcNow;
        device.LastSeenAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var configured = device.OwnerUserId.HasValue;
        var activeBinding = device.InstagramBindings.FirstOrDefault(b => b.Active);
        var igAccount = activeBinding?.InstagramAccount;

        var instagramConnected = igAccount != null && igAccount.ConnectionStatus != InstagramConnectionStatus.Disconnected;
        var isStale = igAccount != null && igAccount.ConnectionStatus != InstagramConnectionStatus.Connected;

        var followers = igAccount?.FollowerCount ?? 0;
        var sequence = igAccount?.FollowerSequence ?? 0;
        var username = igAccount?.Username;
        var updatedAt = igAccount?.LastFollowerRefreshAt;

        var pollSeconds = isStale ? 60 : 30;

        return new DeviceStateResponseDto(
            DeviceId: device.SerialNumber,
            Configured: configured,
            InstagramConnected: instagramConnected,
            Username: username,
            Followers: followers,
            Sequence: sequence,
            IsStale: isStale,
            UpdatedAt: updatedAt,
            ServerTime: now,
            PollAfterSeconds: pollSeconds
        );
    }

    public async Task<DeviceHeartbeatResponseDto> RecordHeartbeatAsync(Guid deviceId, DeviceHeartbeatRequestDto request, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        var now = DateTimeOffset.UtcNow;
        device.RecordHeartbeat(request.FirmwareVersion, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeviceHeartbeatResponseDto(true, now);
    }
}
