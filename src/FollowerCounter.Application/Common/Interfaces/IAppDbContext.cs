using FollowerCounter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FollowerCounter.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<InstagramAccount> InstagramAccounts { get; }
    DbSet<InstagramOAuthSession> InstagramOAuthSessions { get; }
    DbSet<Device> Devices { get; }
    DbSet<DeviceClaim> DeviceClaims { get; }
    DbSet<DeviceInstagramBinding> DeviceInstagramBindings { get; }
    DbSet<FollowerHistory> FollowerHistories { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SecurityEvent> SecurityEvents { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
