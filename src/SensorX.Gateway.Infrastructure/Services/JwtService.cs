using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IKeyManagementService _keyManager;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly TokenValidationParameters _validationParameters;

    public JwtService(IKeyManagementService keyManager, IConfiguration configuration)
    {
        _keyManager = keyManager;
        _issuer = configuration["JwtSettings:Issuer"] ?? "https://gateway.yourdomain.com";
        _audience = configuration["JwtSettings:Audience"] ?? "api";
        _accessTokenMinutes = configuration.GetValue("JwtSettings:AccessTokenMinutes", 15);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = _keyManager.ResolveSigningKey,
            ClockSkew = TimeSpan.Zero
        };
    }

    public string Sign(IEnumerable<Claim> claims, int? expireMinutes = null)
    {
        var credentials = _keyManager.GetSigningCredentials();
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(expireMinutes ?? _accessTokenMinutes);

        var allClaims = new List<Claim>(claims)
        {
            new("jti", Guid.NewGuid().ToString()),
            new("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var header = new JwtHeader(credentials);
        header["kid"] = _keyManager.GetKid();

        var payload = new JwtPayload(
            issuer: _issuer, audience: _audience,
            claims: allClaims, notBefore: now, expires: expires);

        var token = new JwtSecurityToken(header, payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string tokenString)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(tokenString, _validationParameters, out _);
        }
        catch { return null; }
    }
}
