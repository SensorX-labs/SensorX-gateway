using Microsoft.AspNetCore.Mvc;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Api.Controllers;

[ApiController]
[Route("auth")]
public class TokenController : ControllerBase
{
    private readonly IServiceTokenService _serviceTokenService;

    public TokenController(IServiceTokenService serviceTokenService)
    {
        _serviceTokenService = serviceTokenService;
    }

    [HttpPost("token")]
    public async Task<IActionResult> ClientCredentials([FromBody] ClientCredentialsRequest request)
    {
        if (request.GrantType != "client_credentials")
            return BadRequest(new { message = "Unsupported grant_type" });

        var token = await _serviceTokenService.AuthenticateClient(request.ClientId, request.ClientSecret);
        if (token == null)
            return Unauthorized(new { message = "Invalid client credentials" });

        return Ok(new ServiceTokenResponse(token, "Bearer", 3600));
    }
}
