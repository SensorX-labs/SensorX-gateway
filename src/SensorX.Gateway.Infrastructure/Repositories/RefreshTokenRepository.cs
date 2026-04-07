using Microsoft.EntityFrameworkCore;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenHmacAsync(string tokenHmac)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHmac == tokenHmac);
    }

    public void Add(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt >= DateTimeOffset.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }
    }
}
