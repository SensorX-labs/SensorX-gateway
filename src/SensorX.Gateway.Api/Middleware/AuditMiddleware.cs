using System.Diagnostics;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Services;

namespace SensorX.Gateway.Api.Middleware;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, IAuditLogger auditLogger, PiiSanitizer sanitizer)
    {
        var sw = Stopwatch.StartNew();
        await _next(ctx);
        sw.Stop();

        var userId = ctx.User.FindFirst("sub")?.Value;
        Guid? parsedUserId = Guid.TryParse(userId, out var uid) ? uid : null;

        var entry = new AuditEntry
        {
            EventType = ResolveEventType(ctx),
            UserId = parsedUserId,
            CorrelationId = ctx.Items["CorrelationId"]?.ToString(),
            ClientIp = ctx.Connection.RemoteIpAddress?.ToString(),
            Endpoint = ctx.Request.Path,
            Method = ctx.Request.Method,
            StatusCode = ctx.Response.StatusCode,
            DurationMs = (int)sw.ElapsedMilliseconds,
            EventData = sanitizer.Sanitize(
                ctx.Request.Headers.UserAgent.ToString(),
                ctx.Request.ContentType,
                ctx.Request.QueryString.Value)
        };

        await auditLogger.WriteAsync(entry);
    }

    private static string ResolveEventType(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value?.ToLowerInvariant() ?? "";
        var status = ctx.Response.StatusCode;
        return path switch
        {
            "/auth/login" => "LOGIN",
            "/auth/logout" => "LOGOUT",
            "/auth/refresh" => "TOKEN_REFRESH",
            "/auth/register" => "REGISTER",
            _ when status == 403 => "PERMISSION_DENIED",
            _ when status == 401 => "AUTH_FAILED",
            _ => "REQUEST"
        };
    }
}

public static class AuditMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
        => builder.UseMiddleware<AuditMiddleware>();
}
