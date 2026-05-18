using Yarp.ReverseProxy.Transforms;

namespace SensorX.Gateway.Api.ReverseProxy;

public static class YarpTransformProvider
{
    public static IReverseProxyBuilder AddGatewayTransforms(this IReverseProxyBuilder builder)
    {
        builder.AddTransforms(ctx =>
        {
            ctx.AddRequestTransform(transform =>
            {
                var user = transform.HttpContext.User;

                if (user.Identity?.IsAuthenticated == true)
                {
                    // Inject user context headers for downstream services
                    transform.ProxyRequest.Headers
                        .TryAddWithoutValidation("X-User-Id",
                            user.FindFirst("sub")?.Value ?? "");

                    var role = user.FindFirst("role")?.Value ?? "";
                    transform.ProxyRequest.Headers
                        .TryAddWithoutValidation("X-User-Roles", role);

                    if (role == "WarehouseStaff")
                    {
                        var warehouseIdFromToken = user.FindFirst("warehouse_id")?.Value;
                        if (!string.IsNullOrEmpty(warehouseIdFromToken))
                        {
                            transform.ProxyRequest.Headers.Remove("X-Warehouse-Id");
                            transform.ProxyRequest.Headers
                                .TryAddWithoutValidation("X-Warehouse-Id", warehouseIdFromToken);
                        }
                    }
                }

                // Always forward Correlation ID
                var correlationId = transform.HttpContext.Items["CorrelationId"]?.ToString();
                if (!string.IsNullOrEmpty(correlationId))
                {
                    transform.ProxyRequest.Headers
                        .TryAddWithoutValidation("X-Correlation-Id", correlationId);
                }

                return ValueTask.CompletedTask;
            });
        });

        return builder;
    }
}
