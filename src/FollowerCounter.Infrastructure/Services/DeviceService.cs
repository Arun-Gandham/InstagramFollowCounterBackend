using FluentValidation;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Device;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Exceptions;
using FollowerCounter.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Infrastructure.Services;

public class DeviceService : IDeviceService
{
    private readonly IAppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ISecurityEventService _securityEventService;
    private readonly IValidator<ClaimDeviceRequestDto> _claimValidator;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IAppDbContext dbContext,
        IAuditLogService auditLogService,
        ISecurityEventService securityEventService,
        IValidator<ClaimDeviceRequestDto> claimValidator,
        ILogger<DeviceService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _securityEventService = securityEventService;
        _claimValidator = claimValidator;
        _logger = logger;
    }

    public async Task<ClaimDeviceResponseDto> ClaimDeviceAsync(Guid userId, ClaimDeviceRequestDto request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        await _claimValidator.ValidateAndThrowAsync(request, cancellationToken);

        var cleanSerial = request.SerialNumber.Trim();
        var cleanCode = request.ClaimCode.Trim();

        // Support case-insensitive claim code entry
        var hash1 = CryptoHelper.ComputeSha256Hash(cleanCode);
        var hash2 = CryptoHelper.ComputeSha256Hash(cleanCode.ToUpperInvariant());

        var now = DateTimeOffset.UtcNow;

        // Transactional claim processing using execution strategy to support retries
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var device = await _dbContext.Devices
                .Include(d => d.Claims)
                .FirstOrDefaultAsync(d => d.SerialNumber == cleanSerial, cancellationToken);

            if (device == null)
            {
                await _securityEventService.RecordEventAsync("DeviceNotFoundOnClaim", "Low", ipAddress, details: new { cleanSerial }, cancellationToken: cancellationToken);
                throw new NotFoundException($"Device with serial number '{cleanSerial}' was not found.");
            }

            if (device.OwnerUserId.HasValue)
            {
                await _securityEventService.RecordEventAsync("DoubleClaimAttempt", "Medium", ipAddress, details: new { cleanSerial, userId }, cancellationToken: cancellationToken);
                throw new ConflictException($"Device '{cleanSerial}' is already claimed by an account.");
            }

            var validClaim = device.Claims.FirstOrDefault(c =>
                (CryptoHelper.FixedTimeEquals(c.ClaimTokenHash, hash1) || CryptoHelper.FixedTimeEquals(c.ClaimTokenHash, hash2))
                && c.IsValid(now));

            if (validClaim == null)
            {
                await _securityEventService.RecordEventAsync("InvalidClaimCodeAttempt", "High", ipAddress, details: new { cleanSerial, userId }, cancellationToken: cancellationToken);
                throw new DomainException("The claim code entered is invalid, already used, or expired.");
            }

            device.Claim(userId, now);
            validClaim.MarkUsed(userId, now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditLogService.LogAsync("DeviceClaimed", "Success", userId, deviceId: device.Id, ipAddress: ipAddress, metadata: new { device.SerialNumber }, cancellationToken: cancellationToken);

            return new ClaimDeviceResponseDto(device.Id, device.SerialNumber, device.ClaimedAt!.Value);
        });
    }

    public async Task<IReadOnlyList<DeviceDto>> GetUserDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var devices = await _dbContext.Devices
            .Where(d => d.OwnerUserId == userId)
            .Include(d => d.InstagramBindings)
                .ThenInclude(b => b.InstagramAccount)
            .OrderByDescending(d => d.ClaimedAt)
            .ToListAsync(cancellationToken);

        return devices.Select(d =>
        {
            var activeBinding = d.InstagramBindings.FirstOrDefault(b => b.Active);
            DeviceLinkedInstagramDto? linkedIg = null;
            if (activeBinding?.InstagramAccount != null)
            {
                linkedIg = new DeviceLinkedInstagramDto(
                    activeBinding.InstagramAccount.Id,
                    activeBinding.InstagramAccount.Username,
                    activeBinding.InstagramAccount.FollowerCount,
                    activeBinding.InstagramAccount.ConnectionStatus
                );
            }

            return new DeviceDto(
                Id: d.Id,
                SerialNumber: d.SerialNumber,
                Status: d.Status,
                FirmwareVersion: d.FirmwareVersion,
                LastSeenAt: d.LastSeenAt,
                ClaimedAt: d.ClaimedAt,
                CreatedAt: d.CreatedAt,
                LinkedInstagramAccount: linkedIg
            );
        }).ToList();
    }

    public async Task BindInstagramAccountAsync(Guid userId, Guid deviceId, Guid instagramAccountId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.InstagramBindings)
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.OwnerUserId == userId, cancellationToken);

        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        var instagramAccount = await _dbContext.InstagramAccounts
            .FirstOrDefaultAsync(a => a.Id == instagramAccountId && a.OwnerUserId == userId, cancellationToken);

        if (instagramAccount == null)
        {
            throw new NotFoundException(nameof(InstagramAccount), instagramAccountId);
        }

        var now = DateTimeOffset.UtcNow;

        // Deactivate existing active bindings for this device
        foreach (var existingBinding in device.InstagramBindings.Where(b => b.Active))
        {
            existingBinding.Active = false;
            existingBinding.UpdatedAt = now;
        }

        var newBinding = new DeviceInstagramBinding
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            InstagramAccountId = instagramAccount.Id,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.DeviceInstagramBindings.Add(newBinding);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync("DeviceBoundToInstagram", "Success", userId, deviceId: device.Id, ipAddress: ipAddress, metadata: new { instagramAccount.Username }, cancellationToken: cancellationToken);
    }

    public async Task UnbindInstagramAccountAsync(Guid userId, Guid deviceId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var device = await _dbContext.Devices
            .Include(d => d.InstagramBindings)
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.OwnerUserId == userId, cancellationToken);

        if (device == null)
        {
            throw new NotFoundException(nameof(Device), deviceId);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var binding in device.InstagramBindings.Where(b => b.Active))
        {
            binding.Active = false;
            binding.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("DeviceUnboundFromInstagram", "Success", userId, deviceId: device.Id, ipAddress: ipAddress, cancellationToken: cancellationToken);
    }
}
