using SensorX.Gateway.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SensorX.Gateway.Api.HealthChecks;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddGatewayHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgresql", HealthStatus.Unhealthy)
            .AddCheck("self", () => HealthCheckResult.Healthy("Gateway is running"));
        return services;
    }
}
