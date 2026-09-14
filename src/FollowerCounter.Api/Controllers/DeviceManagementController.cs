using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Device;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FollowerCounter.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DeviceManagementController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ICurrentUserService _currentUserService;

    public DeviceManagementController(IDeviceService deviceService, ICurrentUserService currentUserService)
    {
        _deviceService = deviceService;
        _currentUserService = currentUserService;
    }

    [HttpPost("claim")]
    [EnableRateLimiting("DeviceClaimPolicy")]
    [ProducesResponseType(typeof(ClaimDeviceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClaimDevice([FromBody] ClaimDeviceRequestDto request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _deviceService.ClaimDeviceAsync(_currentUserService.UserId.Value, request, ip, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserDevices(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var devices = await _deviceService.GetUserDevicesAsync(_currentUserService.UserId.Value, cancellationToken);
        return Ok(devices);
    }

    [HttpPost("{deviceId:guid}/instagram/{instagramAccountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BindInstagram([FromRoute] Guid deviceId, [FromRoute] Guid instagramAccountId, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _deviceService.BindInstagramAccountAsync(_currentUserService.UserId.Value, deviceId, instagramAccountId, ip, cancellationToken);
        return Ok(new { message = "Device successfully linked to Instagram account." });
    }

    [HttpDelete("{deviceId:guid}/instagram")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnbindInstagram([FromRoute] Guid deviceId, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _deviceService.UnbindInstagramAccountAsync(_currentUserService.UserId.Value, deviceId, ip, cancellationToken);
        return Ok(new { message = "Device unlinked from Instagram." });
    }
}
