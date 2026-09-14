using FollowerCounter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FollowerCounter.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext dbContext, IConfiguration configuration, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "Live", timestamp = DateTimeOffset.UtcNow });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, object>();
        var isHealthy = true;

        // 1. PostgreSQL check
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            checks["database"] = canConnect ? "Connected" : "Unreachable";
            if (!canConnect) isHealthy = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check database query failed.");
            checks["database"] = "Error";
            isHealthy = false;
        }

        // 2. Secret Protection check
        var activeKeyId = _configuration.GetValue<string>("SecretProtection:ActiveKeyId");
        var activeKey = _configuration.GetValue<string>($"SecretProtection:Keys:{activeKeyId}");
        var keyConfigured = !string.IsNullOrWhiteSpace(activeKeyId) && !string.IsNullOrWhiteSpace(activeKey);
        checks["secretProtection"] = keyConfigured ? "Configured" : "MissingActiveKey";
        if (!keyConfigured) isHealthy = false;

        // 3. Instagram configuration check
        var provider = _configuration.GetValue<string>("Instagram:Provider");
        checks["instagramProvider"] = string.Equals(provider, "Fake", StringComparison.OrdinalIgnoreCase) ? "Fake (Development)" : "Meta Graph API";

        checks["serverTime"] = DateTimeOffset.UtcNow;

        if (!isHealthy)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", checks });
        }

        return Ok(new { status = "Healthy", checks });
    }
}
