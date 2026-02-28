namespace SensorX.Gateway.Domain.Interfaces;

public interface IRefreshTokenService
{
    Task<string> CreateAsync(Guid userId, int expiryDays = 30);
    Task<(Guid UserId, string NewRawToken)> RefreshAsync(string rawToken);
    Task RevokeAsync(string rawToken);
    Task RevokeAllForUserAsync(Guid userId);
}
