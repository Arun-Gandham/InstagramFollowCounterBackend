using System.Net;
using System.Net.Sockets;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Domain.Entities;
using FollowerCounter.Infrastructure.Development;
using FollowerCounter.Infrastructure.Email;
using FollowerCounter.Infrastructure.Meta;
using FollowerCounter.Infrastructure.Persistence;
using FollowerCounter.Infrastructure.Security;
using FollowerCounter.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FollowerCounter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // 1. PostgreSQL EF Core DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // 2. ASP.NET Core Identity
        services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // 3. Persistent Data Protection (stored in DB for multi-instance / restart resilience)
        services.AddDataProtection()
            .SetApplicationName("FollowerCounter")
            .PersistKeysToDbContext<AppDbContext>();

        // 4. Token Encryption (AES-256-GCM)
        services.Configure<SecretProtectionOptions>(configuration.GetSection(SecretProtectionOptions.SectionName));
        services.AddSingleton<ISecretProtector, SecretProtector>();

        // 5. Email Services
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var emailProvider = configuration.GetValue<string>("Email:Provider") ?? "Console";
        if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, ConsoleEmailService>();
        }

        // 6. Instagram Provider (Fake for Dev/Testing or Official Meta)
        services.Configure<MetaOptions>(configuration.GetSection(MetaOptions.SectionName));
        var instagramProviderType = configuration.GetValue<string>("Instagram:Provider");
        var useFake = string.Equals(instagramProviderType, "Fake", StringComparison.OrdinalIgnoreCase)
                      || (string.IsNullOrEmpty(instagramProviderType) && environment.IsDevelopment());

        if (useFake)
        {
            services.AddSingleton<IInstagramProvider, FakeInstagramProvider>();
        }
        else
        {
            services.AddHttpClient<IInstagramProvider, MetaInstagramProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Force IPv4 resolution to prevent Windows/ISP dropping IPv6 packets to Meta servers
                ConnectCallback = async (context, cancellationToken) =>
                {
                    IPAddress[] addresses;
                    try
                    {
                        var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
                        addresses = entry.AddressList;
                    }
                    catch
                    {
                        addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                    }

                    var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                             ?? addresses.FirstOrDefault()
                             ?? throw new SocketException((int)SocketError.HostNotFound);

                    var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                },
                ConnectTimeout = TimeSpan.FromSeconds(10)
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
            });
        }

        // 7. Core Services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ISecurityEventService, SecurityEventService>();

        // 8. Application Business Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IInstagramService, InstagramService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceApiService, DeviceApiService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
