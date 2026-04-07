using SensorX.Gateway.Domain.Entities;

namespace SensorX.Gateway.Domain.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHmacAsync(string tokenHmac);
    void Add(RefreshToken refreshToken);
    Task RevokeAllForUserAsync(Guid userId);
}
