using FluentValidation;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Admin;
using FollowerCounter.Application.DTOs.Common;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly IAppDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<CreateDeviceRequestDto> _createDeviceValidator;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IAppDbContext dbContext,
        UserManager<AppUser> userManager,
        IAuditLogService auditLogService,
        IValidator<CreateDeviceRequestDto> createDeviceValidator,
        ILogger<AdminService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _createDeviceValidator = createDeviceValidator;
        _logger = logger;
    }

    public async Task<PagedResultDto<AdminUserDto>> GetUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbContext.Users.CountAsync(cancellationToken);

        var users = await _dbContext.Users
            .Include(u => u.Devices)
            .Include(u => u.InstagramAccounts)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new AdminUserDto(
            Id: u.Id,
            Email: u.Email!,
            DisplayName: u.DisplayName,
            EmailConfirmed: u.EmailConfirmed,
            Status: u.Status,
            CreatedAt: u.CreatedAt,
            LastLoginAt: u.LastLoginAt,
            DeviceCount: u.Devices.Count,
            InstagramAccountCount: u.InstagramAccounts.Count
        )).ToList();

        return new PagedResultDto<AdminUserDto>(items, totalCount, page, pageSize);
    }

    public async Task<PagedResultDto<AdminDeviceDto>> GetDevicesAsync(int page = 1, int pageSize = 50, string? search = null, DeviceStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Devices.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(d => 
                d.SerialNumber.ToLower().Contains(search) || 
                d.Id.ToString().Contains(search) ||
                (d.OwnerUser != null && d.OwnerUser.Email.ToLower().Contains(search))
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var devices = await query
            .Include(d => d.OwnerUser)
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = devices.Select(d => new AdminDeviceDto(
            Id: d.Id,
            SerialNumber: d.SerialNumber,
            DigitCount: d.DigitCount,
            Status: d.Status,
            FirmwareVersion: d.FirmwareVersion,
            OwnerUserId: d.OwnerUserId,
            OwnerEmail: d.OwnerUser?.Email,
            CreatedAt: d.CreatedAt,
            ClaimedAt: d.ClaimedAt,
            LastSeenAt: d.LastSeenAt
        )).ToList();

        return new PagedResultDto<AdminDeviceDto>(items, totalCount, page, pageSize);
    }

    public async Task<CreateDeviceResponseDto> CreateDeviceAsync(CreateDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _createDeviceValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existing = await _dbContext.Devices.AnyAsync(d => d.SerialNumber == request.SerialNumber.Trim(), cancellationToken);
        if (existing)
        {
            throw new ConflictException($"Device with serial number '{request.SerialNumber}' already exists.");
        }

        var plaintextDeviceSecret = CryptoHelper.GenerateSecureToken(32); // 256 bits
        var plaintextClaimCode = CryptoHelper.GenerateClaimCode();

        var credentialHash = CryptoHelper.ComputeSha256Hash(plaintextDeviceSecret);
        var claimHash = CryptoHelper.ComputeSha256Hash(plaintextClaimCode);

        var now = DateTimeOffset.UtcNow;
        var claimExpiresAt = now.AddYears(2);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            SerialNumber = request.SerialNumber.Trim(),
            DigitCount = request.DigitCount == 5 ? 5 : 7,
            Status = DeviceStatus.Unclaimed,
            CredentialHash = credentialHash,
            CreatedAt = now,
            UpdatedAt = now
        };

        var claim = new DeviceClaim
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ClaimTokenHash = claimHash,
            CreatedAt = now,
            ExpiresAt = claimExpiresAt
        };

        _dbContext.Devices.Add(device);
        _dbContext.DeviceClaims.Add(claim);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync("DeviceProvisioned", "Success", deviceId: device.Id, ipAddress: ipAddress, metadata: new { device.SerialNumber, device.DigitCount }, cancellationToken: cancellationToken);

        return new CreateDeviceResponseDto(
            DeviceId: device.Id,
            SerialNumber: device.SerialNumber,
            DigitCount: device.DigitCount,
            PlaintextDeviceSecret: plaintextDeviceSecret,
            PlaintextClaimCode: plaintextClaimCode,
            ClaimExpiresAt: claimExpiresAt
        );
    }

    public async Task<AdminDeviceDto> UpdateDeviceAsync(Guid deviceId, UpdateDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.OwnerUser)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        if (request.DigitCount.HasValue)
        {
            device.DigitCount = request.DigitCount.Value == 5 ? 5 : 7;
        }

        if (!string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            var trimmedSerial = request.SerialNumber.Trim();
            if (trimmedSerial != device.SerialNumber)
            {
                var exists = await _dbContext.Devices.AnyAsync(d => d.SerialNumber == trimmedSerial && d.Id != deviceId, cancellationToken);
                if (exists)
                {
                    throw new ConflictException($"Device with serial number '{trimmedSerial}' already exists.");
                }
                device.SerialNumber = trimmedSerial;
            }
        }

        device.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "DeviceUpdated",
            "Success",
            deviceId: deviceId,
            ipAddress: ipAddress,
            metadata: new { device.SerialNumber, device.DigitCount },
            cancellationToken: cancellationToken
        );

        return new AdminDeviceDto(
            Id: device.Id,
            SerialNumber: device.SerialNumber,
            DigitCount: device.DigitCount,
            Status: device.Status,
            FirmwareVersion: device.FirmwareVersion,
            OwnerUserId: device.OwnerUserId,
            OwnerEmail: device.OwnerUser?.Email,
            CreatedAt: device.CreatedAt,
            ClaimedAt: device.ClaimedAt,
            LastSeenAt: device.LastSeenAt
        );
    }

    public async Task DisableDeviceAsync(Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        device.Status = DeviceStatus.Disabled;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync("DeviceDisabled", "Success", deviceId: deviceId, ipAddress: ipAddress, cancellationToken: cancellationToken);
    }

    public async Task<string> ResetClaimAsync(Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.Claims)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        var newClaimCode = CryptoHelper.GenerateClaimCode();
        var claimHash = CryptoHelper.ComputeSha256Hash(newClaimCode);

        var now = DateTimeOffset.UtcNow;
        var newClaim = new DeviceClaim
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ClaimTokenHash = claimHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(30)
        };

        _dbContext.DeviceClaims.Add(newClaim);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync("DeviceClaimReset", "Success", deviceId: deviceId, ipAddress: ipAddress, cancellationToken: cancellationToken);
        return newClaimCode;
    }

    public async Task<IReadOnlyList<AdminInstagramConnectionDto>> GetInstagramConnectionsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.InstagramAccounts
            .Include(a => a.OwnerUser)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return accounts.Select(a => new AdminInstagramConnectionDto(
            Id: a.Id,
            InstagramUserId: a.InstagramUserId,
            Username: a.Username,
            AccountType: a.AccountType,
            OwnerUserId: a.OwnerUserId,
            OwnerEmail: a.OwnerUser.Email!,
            ConnectionStatus: a.ConnectionStatus,
            RequiresReauthorization: a.RequiresReauthorization,
            FollowerCount: a.FollowerCount,
            FollowerSequence: a.FollowerSequence,
            LastFollowerRefreshAt: a.LastFollowerRefreshAt,
            NextRefreshAttemptAt: a.NextRefreshAttemptAt,
            TokenRefreshFailureCount: a.TokenRefreshFailureCount,
            LastApiErrorCode: a.LastApiErrorCode,
            LastApiErrorAt: a.LastApiErrorAt
        )).ToList();
    }

    public async Task<AdminSystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
        var totalActiveDevices = await _dbContext.Devices.CountAsync(d => d.Status == DeviceStatus.Active, cancellationToken);
        var totalInstagram = await _dbContext.InstagramAccounts.CountAsync(a => a.ConnectionStatus == InstagramConnectionStatus.Connected, cancellationToken);
        var reauth = await _dbContext.InstagramAccounts.CountAsync(a => a.RequiresReauthorization, cancellationToken);
        var rateLimited = await _dbContext.InstagramAccounts.CountAsync(a => a.ConnectionStatus == InstagramConnectionStatus.RateLimited, cancellationToken);

        return new AdminSystemHealthDto(
            Status: "Healthy",
            TotalUsers: totalUsers,
            TotalActiveDevices: totalActiveDevices,
            TotalConnectedInstagramAccounts: totalInstagram,
            AccountsRequiringReauth: reauth,
            RateLimitedAccounts: rateLimited,
            ServerTime: DateTimeOffset.UtcNow
        );
    }
}
