using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Persistence;
using SensorX.Gateway.Infrastructure.Services;

namespace SensorX.Gateway.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ── Database (PostgreSQL) ──
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // ── Redis ──
        var redisConnectionString = configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        // ── Key Management (registered by Program.cs, map interface here) ──
        services.AddSingleton<IKeyManagementService>(sp =>
            sp.GetRequiredService<KeyManagementService>());

        // ── Domain services ──
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IRedisPermissionService, RedisPermissionService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenBlacklistService, RedisTokenBlacklistService>();

        // ── Application services ──
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<IServiceTokenService, ServiceTokenService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();

        // ── Audit (background service) ──
        services.AddSingleton<AuditLogger>();
        services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<AuditLogger>());
        services.AddHostedService(sp => sp.GetRequiredService<AuditLogger>());
        services.AddSingleton<PiiSanitizer>();

        // ── Memory cache (for claims enrichment) ──
        services.AddMemoryCache();

        return services;
    }
}
