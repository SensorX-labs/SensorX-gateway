using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Infrastructure.Persistence;
using SensorX.Gateway.Infrastructure.Repositories;
using SensorX.Gateway.Infrastructure.Services;
using SensorX.Gateway.Application.Services;

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


        // ── Domain services ──
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IRedisPermissionService, RedisPermissionService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // ── Repositories ──
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Application services ──
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();

        // ── Memory cache (for claims enrichment) ──
        services.AddMemoryCache();

        return services;
    }
}
