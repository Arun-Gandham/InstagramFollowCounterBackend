using System.Security.Claims;
using System.Text.Encodings.Web;
using FollowerCounter.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FollowerCounter.Api.Authentication;

public class DeviceAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "Device";
}

public class DeviceAuthenticationHandler : AuthenticationHandler<DeviceAuthenticationOptions>
{
    private readonly IDeviceApiService _deviceApiService;

    public DeviceAuthenticationHandler(
        IOptionsMonitor<DeviceAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDeviceApiService deviceApiService)
        : base(options, logger, encoder)
    {
        _deviceApiService = deviceApiService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Extract device serial number from headers or query param
        string? serialNumber = null;
        if (Request.Headers.TryGetValue("X-Device-Serial", out var serialHeader))
        {
            serialNumber = serialHeader.ToString();
        }
        else if (Request.Headers.TryGetValue("X-Device-Id", out var idHeader))
        {
            serialNumber = idHeader.ToString();
        }
        else if (Request.Query.TryGetValue("serial", out var serialQuery))
        {
            serialNumber = serialQuery.ToString();
        }

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return AuthenticateResult.NoResult();
        }

        // 2. Extract device secret token from headers or query param
        string? secret = null;
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authHeaderStr = authHeader.ToString();
            if (authHeaderStr.StartsWith("Device ", StringComparison.OrdinalIgnoreCase))
            {
                secret = authHeaderStr[7..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(secret) && Request.Headers.TryGetValue("X-Device-Token", out var tokenHeader))
        {
            secret = tokenHeader.ToString();
        }
        else if (string.IsNullOrWhiteSpace(secret) && Request.Query.TryGetValue("secret", out var secretQuery))
        {
            secret = secretQuery.ToString();
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return AuthenticateResult.Fail("Device credentials were not provided.");
        }

        // 3. Authenticate against database using constant-time hash comparison
        var device = await _deviceApiService.AuthenticateDeviceAsync(serialNumber, secret);
        if (device == null)
        {
            return AuthenticateResult.Fail("Invalid device credentials or device is disabled.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim(ClaimTypes.Name, device.SerialNumber),
            new Claim(ClaimTypes.Role, "Device")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
