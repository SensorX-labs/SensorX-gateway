namespace SensorX.Gateway.Domain.Interfaces;

public interface ITokenBlacklistService
{
    /// <summary>
    /// Thêm JWT ID (jti) vào blacklist với TTL tương ứng với thời gian còn lại của token
    /// </summary>
    Task BlacklistAsync(string jti, TimeSpan ttl);

    /// <summary>
    /// Kiểm tra xem JWT ID có bị blacklist hay không
    /// </summary>
    Task<bool> IsBlacklistedAsync(string jti);
}