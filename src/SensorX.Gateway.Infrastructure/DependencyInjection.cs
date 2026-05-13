using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Application.Services;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Infrastructure.Persistence;
using SensorX.Gateway.Infrastructure.Repositories;
using SensorX.Gateway.Infrastructure.Services;
using StackExchange.Redis;

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
        services.AddScoped<DbSeeder>();

        // ── Memory cache (for claims enrichment) ──
        services.AddMemoryCache();

        // ── RabbitMQ / MassTransit ──
        services.AddMassTransit(x =>
        {
            // Đăng ký Entity Framework Outbox
            x.AddEntityFrameworkOutbox<AppDbContext>(o =>
            {
                // Sử dụng Postgres
                o.UsePostgres();

                // Quan trọng: Báo cho MassTransit biết hãy đóng vai trò là Outbox
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqHost = configuration["RabbitMQ:Host"] ?? "localhost";
                var rabbitMqPort = configuration.GetValue<ushort>("RabbitMQ:Port", 5672);
                var rabbitMqVHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

                cfg.Host(rabbitMqHost, rabbitMqPort, rabbitMqVHost, h =>
                {
                    h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                    h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                });

                // Đổi tên Exchange thay vì dùng tên mặc định của MassTransit
                cfg.Message<SensorX.Gateway.Application.Commands.CreateAccount.CreateAccountEvent>(e =>
                    e.SetEntityName("account-created"));

                cfg.Message<SensorX.Gateway.Application.Commands.CustomerRegisterAccount.CustomerRegisterAccountEvent>(e =>
                    e.SetEntityName("customer-registered"));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
