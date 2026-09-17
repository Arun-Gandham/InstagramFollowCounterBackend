using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FollowerCounter.Api.Authentication;
using FollowerCounter.Api.Data;
using FollowerCounter.Api.Middlewares;
using FollowerCounter.Api.Services;
using FollowerCounter.Application;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Infrastructure;
using FollowerCounter.Infrastructure.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Fast-fail startup configuration validation
ValidateConfiguration(builder.Configuration, builder.Environment);

// 1. Application and Infrastructure Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddBackgroundWorkers(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// 2. Controllers & JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 3. Authentication: Cookie Scheme for Users + Custom Device Scheme for ESP32 Counters
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
})
.AddScheme<DeviceAuthenticationOptions, DeviceAuthenticationHandler>(DeviceAuthenticationOptions.SchemeName, _ => { });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".FollowerCounter.Auth";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

    // Support cross-origin API calls (e.g. localhost frontend to production backend)
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// 4. Anti-CSRF Protection
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// 5. Rate Limiting Policies
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("RegistrationPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));

    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("ForgotPasswordPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));

    options.AddPolicy("InstagramConnectPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("DeviceClaimPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("DeviceStatePolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("ManualRefreshPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 6. CORS Configuration
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        // Allow configured origins securely with credentials
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition");
    });
});

// 7. OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Follower Counter API",
        Version = "v1",
        Description = "Commercial Physical Instagram Follower Counter Backend API"
    });

    c.AddSecurityDefinition("DeviceAuth", new OpenApiSecurityScheme
    {
        Description = "Device Secret Header for ESP32 Hardware Counters. Format: 'Device <secret>' with header 'X-Device-Serial: <serial>'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Device"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "DeviceAuth"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 8. Middleware Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Route resolution and CORS preflight handling MUST run first
app.UseRouting();
app.UseCors("DefaultCors");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Follower Counter API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

// 9. Development Seed Data Execution
await DbInitializer.SeedAsync(app.Services, app.Environment, app.Logger);

app.Run();

// Fast-fail startup validation method
static void ValidateConfiguration(IConfiguration configuration, IHostEnvironment env)
{
    var connStr = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connStr))
    {
        throw new InvalidOperationException("Startup failure: 'ConnectionStrings:DefaultConnection' is missing.");
    }

    var activeKeyId = configuration.GetValue<string>("SecretProtection:ActiveKeyId");
    var activeKey = configuration.GetValue<string>($"SecretProtection:Keys:{activeKeyId}");
    if (string.IsNullOrWhiteSpace(activeKeyId) || string.IsNullOrWhiteSpace(activeKey))
    {
        throw new InvalidOperationException("Startup failure: 'SecretProtection' active master key is not configured.");
    }

    if (env.IsProduction())
    {
        var clientId = configuration.GetValue<string>("Meta:ClientId");
        var clientSecret = configuration.GetValue<string>("Meta:ClientSecret");
        var redirectUri = configuration.GetValue<string>("Meta:RedirectUri");

        var provider = configuration.GetValue<string>("Instagram:Provider");
        if (!string.Equals(provider, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new InvalidOperationException("Startup failure: Production requires 'Meta:ClientId', 'Meta:ClientSecret', and 'Meta:RedirectUri' to be configured.");
            }
        }
    }
}

public partial class Program { }
