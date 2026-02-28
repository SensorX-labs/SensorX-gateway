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

                    transform.ProxyRequest.Headers
                        .TryAddWithoutValidation("X-User-Roles",
                            user.FindFirst("role")?.Value ?? "");
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
