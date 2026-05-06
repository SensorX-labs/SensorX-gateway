using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorX.Gateway.Application.Commands.CreateAccount;
using SensorX.Gateway.Application.Commands.CustomerRegisterAccount;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IMediator _mediator;

    public AuthController(IAuthService authService, IMediator mediator)
    {
        _authService = authService;
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshAsync(request);
        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var userIdString = User.FindFirst("sub")?.Value;
        var result = await _authService.LogoutAsync(userIdString, request);
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CustomerRegisterAccountCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
            return Conflict(result);

        return Created("", result);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateStaffAccount([FromBody] CreateAccountCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
            return Conflict(result);

        return Created("", result);
    }

    [Authorize]
    [HttpPost("introspect")]
    public IActionResult Introspect([FromBody] IntrospectRequest request)
    {
        var result = _authService.Introspect(request);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke()
    {
        var userIdString = User.FindFirst("sub")?.Value;
        var result = await _authService.RevokeAsync(userIdString);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdString = User.FindFirst("sub")?.Value;
        var result = await _authService.ChangePasswordAsync(userIdString, request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // [Authorize(Roles = "Manager,Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var result = await _authService.GetAllUsersAsync();
        return Ok(result);
    }

    [Authorize(Roles = "Manager,Admin")]
    [HttpPost("users/{id}/toggle-lock")]
    public async Task<IActionResult> ToggleUserLock(Guid id)
    {
        var result = await _authService.ToggleUserLockAsync(id);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }
}
