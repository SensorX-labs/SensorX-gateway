using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly string _hmacSecret;

    public RefreshTokenService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _hmacSecret = configuration["JwtSettings:HmacSecret"]
            ?? throw new InvalidOperationException("JwtSettings:HmacSecret is required");
    }

    public async Task<string> CreateAsync(Guid userId, int expiryDays = 30)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hmac = ComputeHmac(raw);

        await _db.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = userId,
            TokenHmac = hmac,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays)
        });
        await _db.SaveChangesAsync();
        return raw;
    }

    public async Task<(Guid UserId, string NewRawToken)> RefreshAsync(string rawToken)
    {
        var hmac = ComputeHmac(rawToken);
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHmac == hmac);

        if (storedToken == null)
            throw new InvalidOperationException("INVALID_TOKEN");

        if (storedToken.IsRevoked)
        {
            await RevokeAllForUserAsync(storedToken.UserId);
            throw new InvalidOperationException("REUSE_DETECTED");
        }

        if (storedToken.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("TOKEN_EXPIRED");

        storedToken.IsRevoked = true;
        storedToken.LastUsedAt = DateTimeOffset.UtcNow;

        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var newHmac = ComputeHmac(newRaw);

        await _db.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = storedToken.UserId,
            TokenHmac = newHmac,
            ExpiresAt = storedToken.ExpiresAt
        });

        await _db.SaveChangesAsync();
        return (storedToken.UserId, newRaw);
    }

    public async Task RevokeAsync(string rawToken)
    {
        var hmac = ComputeHmac(rawToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHmac == hmac);

        if (token != null)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
            token.IsRevoked = true;

        await _db.SaveChangesAsync();
    }

    private string ComputeHmac(string rawToken)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_hmacSecret);
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = HMACSHA256.HashData(keyBytes, tokenBytes);
        return Convert.ToBase64String(hash);
    }
}
