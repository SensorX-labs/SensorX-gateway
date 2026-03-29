using System.Security.Claims;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class AccessTokenService : IAccessTokenService
{
    private readonly IJwtService _jwtService;

    public AccessTokenService(IJwtService jwtService)
    {
        _jwtService = jwtService;
    }

    public string CreateToken(Guid userId, string email, string role, string scope)
    {
        var username = email.Split('@')[0];
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("name", username),
            new("unique_name", username),
            new("email", email),
            new("role", role),
            new("scope", scope)
        };
        return _jwtService.Sign(claims);
    }

    public string CreateMfaToken(Guid userId)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("purpose", "mfa")
        };
        return _jwtService.Sign(claims, expireMinutes: 5);
    }
}
