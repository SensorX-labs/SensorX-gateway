using StackExchange.Redis;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisTokenBlacklistService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task BlacklistAsync(string jti, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        var key = $"blacklist:jti:{jti}";
        await db.StringSetAsync(key, "1", ttl);
    }

    public async Task<bool> IsBlacklistedAsync(string jti)
    {
        var db = _redis.GetDatabase();
        var key = $"blacklist:jti:{jti}";
        var exists = await db.StringGetAsync(key);
        return !exists.IsNull;
    }
}