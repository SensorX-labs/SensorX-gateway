using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Persistence;
using System.Security.Claims;

namespace SensorX.Gateway.Infrastructure.Services;

public class ServiceTokenService : IServiceTokenService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly string _hmacSecret;

    public ServiceTokenService(AppDbContext db, IJwtService jwtService, IConfiguration configuration)
    {
        _db = db;
        _jwtService = jwtService;
        _hmacSecret = configuration["JwtSettings:HmacSecret"]
            ?? throw new InvalidOperationException("JwtSettings:HmacSecret is required");
    }

    public async Task<string?> AuthenticateClient(string clientId, string clientSecret)
    {
        var client = await _db.ServiceClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);

        if (client == null) return null;

        var secretHash = ComputeHmac(clientSecret);
        if (secretHash != client.ClientSecretHash) return null;

        var claims = new List<Claim>
        {
            new("sub", client.ClientId),
            new("aud", "internal"),
            new("scope", client.Scope)
        };

        return _jwtService.Sign(claims, expireMinutes: 60);
    }

    private string ComputeHmac(string value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_hmacSecret);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var hash = HMACSHA256.HashData(keyBytes, valueBytes);
        return Convert.ToBase64String(hash);
    }
}
