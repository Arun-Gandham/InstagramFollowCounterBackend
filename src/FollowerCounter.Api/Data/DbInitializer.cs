using System.Security.Claims;
using FollowerCounter.Domain.Constants;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Domain.Enums;
using FollowerCounter.Infrastructure.Persistence;
using FollowerCounter.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FollowerCounter.Api.Data;

public static class DbInitializer
{
    private static readonly SemaphoreSlim _seedLock = new(1, 1);

    public static async Task SeedAsync(IServiceProvider serviceProvider, IHostEnvironment env, ILogger logger)
    {
        await _seedLock.WaitAsync();
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            // 1. Ensure all Roles exist and have all granular permissions attached
            foreach (var roleName in AppRoles.All)
            {
                var role = await roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    role = new AppRole(roleName);
                    try
                    {
                        await roleManager.CreateAsync(role);
                        logger.LogInformation("Seeded role: {Role}", roleName);
                    }
                    catch (Exception)
                    {
                        role = await roleManager.FindByNameAsync(roleName);
                    }
                }

                if (role != null)
                {
                    var existingRoleClaims = await roleManager.GetClaimsAsync(role);
                    var existingClaimValues = existingRoleClaims
                        .Where(c => c.Type == AppPermissions.ClaimType)
                        .Select(c => c.Value)
                        .ToHashSet();

                    foreach (var permission in AppPermissions.All)
                    {
                        if (!existingClaimValues.Contains(permission))
                        {
                            try
                            {
                                await roleManager.AddClaimAsync(role, new Claim(AppPermissions.ClaimType, permission));
                            }
                            catch (Exception)
                            {
                                // Handled if inserted concurrently
                            }
                        }
                    }
                }
            }

            // 2. Define users to seed (Super Admin + test user for every role with all permissions)
            var usersToSeed = new (string Email, string Password, string DisplayName, string[] Roles)[]
            {
                (
                    "arunsaigandham1998@gmail.com",
                    "G_arunsai@1998",
                    "Arun Sai Gandham (Super Admin)",
                    [AppRoles.SuperAdmin, AppRoles.Admin]
                ),
                (
                    "admin@counter.local",
                    "AdminPass123!",
                    "Test Administrator",
                    [AppRoles.Admin]
                ),
                (
                    "support@counter.local",
                    "SupportPass123!",
                    "Test Support Agent",
                    [AppRoles.Support]
                ),
                (
                    "customer@counter.local",
                    "CustomerPass123!",
                    "Test Customer",
                    [AppRoles.Customer]
                )
            };

            foreach (var (email, password, displayName, roles) in usersToSeed)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new AppUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = email,
                        Email = email,
                        DisplayName = displayName,
                        EmailConfirmed = true,
                        Status = UserStatus.Active,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    try
                    {
                        var createResult = await userManager.CreateAsync(user, password);
                        if (!createResult.Succeeded)
                        {
                            user = await userManager.FindByEmailAsync(email);
                        }
                        else
                        {
                            logger.LogInformation("Seeded user: {Email}", email);
                        }
                    }
                    catch (Exception)
                    {
                        user = await userManager.FindByEmailAsync(email);
                    }
                }
                else
                {
                    // Ensure email is confirmed and status is active
                    if (!user.EmailConfirmed || user.Status != UserStatus.Active)
                    {
                        user.EmailConfirmed = true;
                        user.Status = UserStatus.Active;
                        try
                        {
                            await userManager.UpdateAsync(user);
                        }
                        catch (Exception)
                        {
                            // Ignored
                        }
                    }
                }

                if (user == null)
                {
                    continue;
                }

                // Assign Roles
                foreach (var role in roles)
                {
                    if (!await userManager.IsInRoleAsync(user, role))
                    {
                        try
                        {
                            await userManager.AddToRoleAsync(user, role);
                        }
                        catch (Exception)
                        {
                            // Ignored
                        }
                    }
                }

                // Grant all permissions directly as user claims
                var existingUserClaims = await userManager.GetClaimsAsync(user);
                var existingUserPermissions = existingUserClaims
                    .Where(c => c.Type == AppPermissions.ClaimType)
                    .Select(c => c.Value)
                    .ToHashSet();

                foreach (var permission in AppPermissions.All)
                {
                    if (!existingUserPermissions.Contains(permission))
                    {
                        try
                        {
                            await userManager.AddClaimAsync(user, new Claim(AppPermissions.ClaimType, permission));
                        }
                        catch (Exception)
                        {
                            // Ignored
                        }
                    }
                }
            }

            logger.LogInformation(
                "====================== [DB SEEDER COMPLETE] ======================\n" +
                "Super Admin: arunsaigandham1998@gmail.com | Roles: [SuperAdmin, Admin]\n" +
                "Admin User:  admin@counter.local           | Role:  [Admin]\n" +
                "Support:     support@counter.local         | Role:  [Support]\n" +
                "Customer:    customer@counter.local        | Role:  [Customer]\n" +
                "All permissions ({Count}) granted to all roles and seeded users.\n" +
                "==================================================================",
                AppPermissions.All.Count);

            // 3. Development Sample Device (for hardware counter testing)
            if (env.IsDevelopment())
            {
                var sampleSerial = "FC-A82F32";
                var sampleSecret = "dev_device_secret_256bit_secure_token_99";
                var sampleClaimCode = "CLM-82F3-2ABC-9999";

                try
                {
                    var existingDevice = await dbContext.Devices.FirstOrDefaultAsync(d => d.SerialNumber == sampleSerial);
                    if (existingDevice == null)
                    {
                        var device = new Device
                        {
                            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                            SerialNumber = sampleSerial,
                            Status = DeviceStatus.Unclaimed,
                            CredentialHash = CryptoHelper.ComputeSha256Hash(sampleSecret),
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };

                        var claim = new DeviceClaim
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = device.Id,
                            ClaimTokenHash = CryptoHelper.ComputeSha256Hash(sampleClaimCode),
                            CreatedAt = DateTimeOffset.UtcNow,
                            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
                        };

                        dbContext.Devices.Add(device);
                        dbContext.DeviceClaims.Add(claim);
                        await dbContext.SaveChangesAsync();

                        logger.LogInformation(
                            "Manufactured Sample Device: {Serial} | Claim Code: {Claim}",
                            sampleSerial, sampleClaimCode);
                    }
                }
                catch (Exception)
                {
                    // Ignore concurrency insertion
                }
            }
        }
        finally
        {
            _seedLock.Release();
        }
    }
}
