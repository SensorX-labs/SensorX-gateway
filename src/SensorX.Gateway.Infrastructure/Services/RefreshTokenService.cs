using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Infrastructure.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _hmacSecret;

    public RefreshTokenService(IRefreshTokenRepository repo, IUnitOfWork unitOfWork, IConfiguration configuration)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _hmacSecret = configuration["JwtSettings:HmacSecret"]
            ?? throw new InvalidOperationException("JwtSettings:HmacSecret is required");
    }

    public async Task<string> CreateAsync(Guid userId, int expiryDays = 30)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hmac = ComputeHmac(raw);

        var rt = RefreshToken.Create(userId, hmac, DateTimeOffset.UtcNow.AddDays(expiryDays));
        _repo.Add(rt);
        await _unitOfWork.SaveChangesAsync();
        return raw;
    }

    public async Task<(Guid UserId, string NewRawToken)> RefreshAsync(string rawToken)
    {
        var hmac = ComputeHmac(rawToken);
        var storedToken = await _repo.GetByTokenHmacAsync(hmac);

        if (storedToken == null)
            throw new InvalidOperationException("INVALID_TOKEN");

        if (storedToken.IsRevoked)
        {
            await RevokeAllForUserAsync(storedToken.UserId);
            throw new InvalidOperationException("REUSE_DETECTED");
        }

        if (storedToken.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("TOKEN_EXPIRED");

        storedToken.MarkAsUsed();

        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var newHmac = ComputeHmac(newRaw);

        var newRt = RefreshToken.Create(storedToken.UserId, newHmac, storedToken.ExpiresAt);
        _repo.Add(newRt);

        await _unitOfWork.SaveChangesAsync();
        return (storedToken.UserId, newRaw);
    }

    public async Task RevokeAsync(string rawToken)
    {
        var hmac = ComputeHmac(rawToken);
        var token = await _repo.GetByTokenHmacAsync(hmac);

        if (token != null)
        {
            token.Revoke();
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        await _repo.RevokeAllForUserAsync(userId);
        await _unitOfWork.SaveChangesAsync();
    }

    private string ComputeHmac(string rawToken)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_hmacSecret);
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = HMACSHA256.HashData(keyBytes, tokenBytes);
        return Convert.ToBase64String(hash);
    }
}
