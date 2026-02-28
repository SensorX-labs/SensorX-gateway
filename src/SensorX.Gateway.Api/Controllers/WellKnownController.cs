using Microsoft.AspNetCore.Mvc;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Api.Controllers;

[ApiController]
public class WellKnownController : ControllerBase
{
    private readonly IKeyManagementService _keyManager;
    private readonly IConfiguration _configuration;

    public WellKnownController(IKeyManagementService keyManager, IConfiguration configuration)
    {
        _keyManager = keyManager;
        _configuration = configuration;
    }

    [HttpGet(".well-known/jwks.json")]
    [ResponseCache(Duration = 300)]
    public IActionResult GetJwks() => Ok(_keyManager.GetJwksResponse());

    [HttpGet(".well-known/openid-configuration")]
    [ResponseCache(Duration = 3600)]
    public IActionResult GetOpenIdConfiguration()
    {
        var issuer = _configuration["JwtSettings:Issuer"] ?? "https://gateway.yourdomain.com";
        return Ok(new
        {
            issuer,
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            authorization_endpoint = $"{issuer}/auth/login",
            token_endpoint = $"{issuer}/auth/token",
            introspection_endpoint = $"{issuer}/auth/introspect",
            revocation_endpoint = $"{issuer}/auth/revoke",
            response_types_supported = new[] { "token" },
            grant_types_supported = new[] { "password", "refresh_token", "client_credentials" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_post" }
        });
    }
}
