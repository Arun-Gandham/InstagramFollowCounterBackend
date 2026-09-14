using System.Security.Claims;
using FollowerCounter.Api.Authentication;
using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Device;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FollowerCounter.Api.Controllers;

[ApiController]
[Route("device/v1")]
[Authorize(AuthenticationSchemes = DeviceAuthenticationOptions.SchemeName)]
[EnableRateLimiting("DeviceStatePolicy")]
public class DeviceApiController : ControllerBase
{
    private readonly IDeviceApiService _deviceApiService;

    public DeviceApiController(IDeviceApiService deviceApiService)
    {
        _deviceApiService = deviceApiService;
    }

    [HttpGet("state")]
    [ProducesResponseType(typeof(DeviceStateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetState(CancellationToken cancellationToken)
    {
        var deviceIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(deviceIdString, out var deviceId))
        {
            return Unauthorized();
        }

        var state = await _deviceApiService.GetDeviceStateAsync(deviceId, cancellationToken);
        return Ok(state);
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(DeviceHeartbeatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordHeartbeat([FromBody] DeviceHeartbeatRequestDto request, CancellationToken cancellationToken)
    {
        var deviceIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(deviceIdString, out var deviceId))
        {
            return Unauthorized();
        }

        var response = await _deviceApiService.RecordHeartbeatAsync(deviceId, request, cancellationToken);
        return Ok(response);
    }
}
