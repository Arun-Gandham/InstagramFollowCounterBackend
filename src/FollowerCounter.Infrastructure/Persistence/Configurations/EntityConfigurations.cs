using FollowerCounter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FollowerCounter.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasMany(u => u.InstagramAccounts)
            .WithOne(a => a.OwnerUser)
            .HasForeignKey(a => a.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Devices)
            .WithOne(d => d.OwnerUser)
            .HasForeignKey(d => d.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.OAuthSessions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InstagramAccountConfiguration : IEntityTypeConfiguration<InstagramAccount>
{
    public void Configure(EntityTypeBuilder<InstagramAccount> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.InstagramUserId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Username).HasMaxLength(100).IsRequired();
        builder.Property(a => a.AccountType).HasMaxLength(50);
        builder.Property(a => a.ConnectionStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(a => a.TokenEncrypted).IsRequired();
        builder.Property(a => a.TokenType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.LastApiErrorCode).HasMaxLength(100);
        builder.Property(a => a.LockedBy).HasMaxLength(100);

        builder.HasIndex(a => a.InstagramUserId);
        builder.HasIndex(a => new { a.OwnerUserId, a.InstagramUserId }).IsUnique();
        builder.HasIndex(a => a.NextRefreshAttemptAt);
        builder.HasIndex(a => a.LockedUntil);

        builder.HasMany(a => a.DeviceBindings)
            .WithOne(b => b.InstagramAccount)
            .HasForeignKey(b => b.InstagramAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.FollowerHistories)
            .WithOne(h => h.InstagramAccount)
            .HasForeignKey(h => h.InstagramAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InstagramOAuthSessionConfiguration : IEntityTypeConfiguration<InstagramOAuthSession>
{
    public void Configure(EntityTypeBuilder<InstagramOAuthSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StateHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.RedirectAfterSuccess).HasMaxLength(500);
        builder.Property(s => s.IpHash).HasMaxLength(64);

        builder.HasIndex(s => s.StateHash).IsUnique();
        builder.HasIndex(s => s.ExpiresAt);
        builder.HasIndex(s => s.UserId);
    }
}

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.SerialNumber).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(d => d.CredentialHash).HasMaxLength(64).IsRequired();
        builder.Property(d => d.FirmwareVersion).HasMaxLength(50);

        builder.HasIndex(d => d.SerialNumber).IsUnique();
        builder.HasIndex(d => d.OwnerUserId);

        builder.HasMany(d => d.Claims)
            .WithOne(c => c.Device)
            .HasForeignKey(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.InstagramBindings)
            .WithOne(b => b.Device)
            .HasForeignKey(b => b.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeviceClaimConfiguration : IEntityTypeConfiguration<DeviceClaim>
{
    public void Configure(EntityTypeBuilder<DeviceClaim> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ClaimTokenHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(c => c.ClaimTokenHash).IsUnique();
        builder.HasIndex(c => c.DeviceId);
        builder.HasIndex(c => c.ExpiresAt);

        builder.HasOne(c => c.UsedByUser)
            .WithMany()
            .HasForeignKey(c => c.UsedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class DeviceInstagramBindingConfiguration : IEntityTypeConfiguration<DeviceInstagramBinding>
{
    public void Configure(EntityTypeBuilder<DeviceInstagramBinding> builder)
    {
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => b.DeviceId);
        builder.HasIndex(b => b.InstagramAccountId);

        // Enforce maximum of one active Instagram binding per physical counter
        builder.HasIndex(b => new { b.DeviceId, b.Active })
            .HasFilter("\"Active\" = true")
            .IsUnique();
    }
}

public class FollowerHistoryConfiguration : IEntityTypeConfiguration<FollowerHistory>
{
    public void Configure(EntityTypeBuilder<FollowerHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.HasIndex(h => new { h.InstagramAccountId, h.ChangedAt });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Result).HasMaxLength(50).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(50);
        builder.Property(a => a.IpAddressHash).HasMaxLength(64);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.DeviceId);
    }
}

public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.EventType).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Severity).HasMaxLength(20).IsRequired();
        builder.Property(s => s.SourceIpHash).HasMaxLength(64);
        builder.Property(s => s.CorrelationId).HasMaxLength(50);

        builder.HasIndex(s => s.Timestamp);
        builder.HasIndex(s => s.EventType);
    }
}
