using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class RedisPermissionService : IRedisPermissionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _defaultTtl;

    public RedisPermissionService(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis;
        var ttlMinutes = configuration.GetValue("JwtSettings:AccessTokenMinutes", 15);
        _defaultTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    public async Task SetPermissionsAsync(Guid userId, IReadOnlyList<string> permissions)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(permissions);
        await db.StringSetAsync($"user_permissions:{userId}", json, _defaultTtl);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync($"user_permissions:{userId}");
        if (json.IsNullOrEmpty) return Array.Empty<string>();
        return JsonSerializer.Deserialize<List<string>>(json!) ?? new List<string>();
    }

    public async Task RemovePermissionsAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"user_permissions:{userId}");
    }
}
