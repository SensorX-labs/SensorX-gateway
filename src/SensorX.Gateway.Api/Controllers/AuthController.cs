using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
            return Conflict(result);

        return Created("", result);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateAccount([FromBody] RegisterRequest request)
    {
        var result = await _authService.CreateAccountAsync(request);
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
}
