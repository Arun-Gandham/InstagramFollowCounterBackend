using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Admin;
using FollowerCounter.Infrastructure.Development;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FollowerCounter.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IHostEnvironment _env;

    public AdminController(IAdminService adminService, IHostEnvironment env)
    {
        _adminService = adminService;
        _env = env;
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var users = await _adminService.GetUsersAsync(page, pageSize, cancellationToken);
        return Ok(users);
    }

    [HttpGet("devices")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminDeviceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var devices = await _adminService.GetDevicesAsync(page, pageSize, cancellationToken);
        return Ok(devices);
    }

    [HttpPost("devices")]
    [ProducesResponseType(typeof(CreateDeviceResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _adminService.CreateDeviceAsync(request, ip, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("devices/{deviceId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableDevice([FromRoute] Guid deviceId, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _adminService.DisableDeviceAsync(deviceId, ip, cancellationToken);
        return Ok(new { message = $"Device {deviceId} has been disabled." });
    }

    [HttpPost("devices/{deviceId:guid}/reset-claim")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetClaim([FromRoute] Guid deviceId, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var newCode = await _adminService.ResetClaimAsync(deviceId, ip, cancellationToken);
        return Ok(new { message = "Claim code reset successfully.", newClaimCode = newCode });
    }

    [HttpGet("instagram-connections")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminInstagramConnectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstagramConnections([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var connections = await _adminService.GetInstagramConnectionsAsync(page, pageSize, cancellationToken);
        return Ok(connections);
    }

    [HttpGet("system-health")]
    [ProducesResponseType(typeof(AdminSystemHealthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemHealth(CancellationToken cancellationToken)
    {
        var health = await _adminService.GetSystemHealthAsync(cancellationToken);
        return Ok(health);
    }

    [HttpPost("fake-instagram/increment")]
    [AllowAnonymous] // Developer convenience endpoint for testing counter increments
    public IActionResult IncrementFakeFollowers([FromQuery] long delta = 1)
    {
        if (!_env.IsDevelopment())
        {
            return Forbid();
        }

        FakeInstagramProvider.IncrementFollowerCount(delta);
        return Ok(new
        {
            message = $"Follower count incremented by {delta}.",
            currentFollowerCount = FakeInstagramProvider.CurrentFollowerCount
        });
    }
}
