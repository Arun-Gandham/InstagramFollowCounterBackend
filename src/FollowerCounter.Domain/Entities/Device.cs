using FollowerCounter.Domain.Enums;
using FollowerCounter.Domain.Exceptions;

namespace FollowerCounter.Domain.Entities;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SerialNumber { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public AppUser? OwnerUser { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Unclaimed;
    public string CredentialHash { get; set; } = string.Empty;
    public string? FirmwareVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DeviceClaim> Claims { get; set; } = new List<DeviceClaim>();
    public ICollection<DeviceInstagramBinding> InstagramBindings { get; set; } = new List<DeviceInstagramBinding>();

    public void Claim(Guid userId, DateTimeOffset claimTime)
    {
        if (Status == DeviceStatus.Disabled || Status == DeviceStatus.Revoked)
        {
            throw new DomainException($"Device {SerialNumber} is {Status} and cannot be claimed.");
        }

        if (OwnerUserId.HasValue || Status == DeviceStatus.Active)
        {
            throw new DomainException($"Device {SerialNumber} has already been claimed.");
        }

        OwnerUserId = userId;
        Status = DeviceStatus.Active;
        ClaimedAt = claimTime;
        UpdatedAt = claimTime;
    }

    public void Unclaim(DateTimeOffset timestamp)
    {
        OwnerUserId = null;
        Status = DeviceStatus.Unclaimed;
        ClaimedAt = null;
        UpdatedAt = timestamp;
    }

    public void RecordHeartbeat(string? firmwareVersion, DateTimeOffset timestamp)
    {
        LastSeenAt = timestamp;
        if (!string.IsNullOrWhiteSpace(firmwareVersion))
        {
            FirmwareVersion = firmwareVersion;
        }
        UpdatedAt = timestamp;
    }
}
