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
    public async Task<IActionResult> GetDevices(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50, 
        [FromQuery] string? search = null, 
        [FromQuery] FollowerCounter.Domain.Enums.DeviceStatus? status = null, 
        CancellationToken cancellationToken = default)
    {
        var devices = await _adminService.GetDevicesAsync(page, pageSize, search, status, cancellationToken);
        return Ok(devices);
    }

    [HttpPost("devices/factory-seed")]
    public async Task<IActionResult> SeedDevices([FromQuery] int count = 100, CancellationToken cancellationToken = default)
    {
        var devices = new List<CreateDeviceResponseDto>();
        for (int i = 0; i < count; i++)
        {
            var req = new CreateDeviceRequestDto($"FC-FACTORY-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}", 7);
            var res = await _adminService.CreateDeviceAsync(req, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            devices.Add(res);
        }
        return Ok(new { Message = $"Seeded {count} devices successfully." });
    }

    [HttpPost("devices")]
    [ProducesResponseType(typeof(CreateDeviceResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _adminService.CreateDeviceAsync(request, ip, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPatch("devices/{deviceId:guid}")]
    [ProducesResponseType(typeof(AdminDeviceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDevice([FromRoute] Guid deviceId, [FromBody] UpdateDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var updated = await _adminService.UpdateDeviceAsync(deviceId, request, ip, cancellationToken);
        return Ok(updated);
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
