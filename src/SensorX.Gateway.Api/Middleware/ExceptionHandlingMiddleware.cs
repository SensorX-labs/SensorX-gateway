using System.Net;
using System.Text.Json;

namespace SensorX.Gateway.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            InvalidOperationException { Message: "INVALID_TOKEN" } => (401, "Invalid token"),
            InvalidOperationException { Message: "REUSE_DETECTED" } => (401, "Token reuse detected — all sessions revoked"),
            InvalidOperationException { Message: "TOKEN_EXPIRED" } => (401, "Token expired"),
            UnauthorizedAccessException => (401, "Unauthorized"),
            KeyNotFoundException => (404, "Resource not found"),
            ArgumentException => (400, ex.Message),
            _ => (500, "An internal error occurred")
        };

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.io/{statusCode}",
            title = ((HttpStatusCode)statusCode).ToString(),
            status = statusCode,
            detail = message,
            traceId = ctx.Items["CorrelationId"]?.ToString()
        };

        await ctx.Response.WriteAsJsonAsync(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        => builder.UseMiddleware<ExceptionHandlingMiddleware>();
}
