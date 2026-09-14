using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace FollowerCounter.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IAppDbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<InstagramAccount> InstagramAccounts => Set<InstagramAccount>();
    public DbSet<InstagramOAuthSession> InstagramOAuthSessions => Set<InstagramOAuthSession>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceClaim> DeviceClaims => Set<DeviceClaim>();
    public DbSet<DeviceInstagramBinding> DeviceInstagramBindings => Set<DeviceInstagramBinding>();
    public DbSet<FollowerHistory> FollowerHistories => Set<FollowerHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
