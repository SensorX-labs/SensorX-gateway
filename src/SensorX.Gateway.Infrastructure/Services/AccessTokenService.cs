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

    public string CreateToken(Guid userId, string email, string role, string scope, Guid? warehouseId = null)
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
        if (warehouseId.HasValue)
        {
            claims.Add(new Claim("warehouse_id", warehouseId.Value.ToString()));
        }
        return _jwtService.Sign(claims);
    }
}
