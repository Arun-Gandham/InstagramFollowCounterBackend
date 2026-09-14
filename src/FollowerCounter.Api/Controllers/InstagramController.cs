using FollowerCounter.Application.Common.Interfaces;
using FollowerCounter.Application.DTOs.Instagram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FollowerCounter.Api.Controllers;

[ApiController]
[Route("api/v1/instagram")]
public class InstagramController : ControllerBase
{
    private readonly IInstagramService _instagramService;
    private readonly ICurrentUserService _currentUserService;

    public InstagramController(IInstagramService instagramService, ICurrentUserService currentUserService)
    {
        _instagramService = instagramService;
        _currentUserService = currentUserService;
    }

    [HttpGet("connect")]
    [Authorize]
    [EnableRateLimiting("InstagramConnectPolicy")]
    [ProducesResponseType(typeof(InstagramConnectResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Connect([FromQuery] string? redirectAfterSuccess, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _instagramService.InitiateConnectAsync(_currentUserService.UserId.Value, redirectAfterSuccess, ip, cancellationToken);
        return Ok(response);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing OAuth parameters",
                Detail = "Both 'code' and 'state' query parameters are required."
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var redirectUrl = await _instagramService.HandleCallbackAsync(code, state, ip, cancellationToken);
        return Redirect(redirectUrl);
    }

    [HttpGet("accounts")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<InstagramAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var accounts = await _instagramService.GetAccountsForUserAsync(_currentUserService.UserId.Value, cancellationToken);
        return Ok(accounts);
    }

    [HttpGet("accounts/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(InstagramAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccountById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var account = await _instagramService.GetAccountByIdAsync(_currentUserService.UserId.Value, id, cancellationToken);
        return Ok(account);
    }

    [HttpPost("accounts/{id:guid}/refresh")]
    [Authorize]
    [EnableRateLimiting("ManualRefreshPolicy")]
    [ProducesResponseType(typeof(RefreshFollowerResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RefreshNow([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _instagramService.ManualRefreshAsync(_currentUserService.UserId.Value, id, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("accounts/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DisconnectAccount([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized();
        }

        await _instagramService.DisconnectAccountAsync(_currentUserService.UserId.Value, id, cancellationToken);
        return Ok(new { message = "Instagram account disconnected and tokens revoked successfully." });
    }
}
