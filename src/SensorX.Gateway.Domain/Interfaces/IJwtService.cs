using System.Security.Claims;

namespace SensorX.Gateway.Domain.Interfaces;

public interface IJwtService
{
    string Sign(IEnumerable<Claim> claims, int? expireMinutes = null);
    ClaimsPrincipal? ValidateToken(string tokenString);
}
