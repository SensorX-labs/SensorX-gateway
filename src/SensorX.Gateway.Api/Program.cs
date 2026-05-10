using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SensorX.Gateway.Api.Authorization;
using SensorX.Gateway.Api.HealthChecks;
using SensorX.Gateway.Api.Middleware;
using SensorX.Gateway.Api.ReverseProxy;
using SensorX.Gateway.Application;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure;
using SensorX.Gateway.Infrastructure.Persistence;
using SensorX.Gateway.Infrastructure.Services;
using Serilog;
using Serilog.Formatting.Compact;

// ═══════════════════════════════════════════════════════════════
//  SensorX API Gateway + Identity Provider
// ═══════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine(builder.Environment.IsDevelopment() ? "Running in Development mode" : "Running in Production mode");
// ── Serilog ──
builder.Host.UseSerilog((ctx, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "sensorx-gateway")
    .WriteTo.Console(new CompactJsonFormatter()));

// ── Infrastructure layer (DB, Redis, services) ──
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "https://gateway.sensorx.com",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "SensorX.Warehouse.Api",
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:HmacSecret"] ?? "fallback-secure-secret-key-that-is-long-enough")),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception,
                    "JWT Authentication failed: {Message} | Path: {Path} | Token: {TokenPreview}",
                    context.Exception.Message,
                    context.Request.Path,
                    context.Request.Headers.Authorization.ToString()[..Math.Min(50, context.Request.Headers.Authorization.ToString().Length)]);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var sub = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
                var role = context.Principal?.FindFirst("role")?.Value ?? "unknown";
                logger.LogInformation("JWT validated | User: {UserId} | Role: {Role} | Path: {Path}", sub, role, context.Request.Path);
                return Task.CompletedTask;
            }
        };
    });

// ── Authorization (Redis-based permissions) ──
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ── CORS ──
builder.Services.AddCors(options =>
    options.AddPolicy("Production", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["https://app.yourdomain.com", "https://admin.yourdomain.com", "http://localhost:3000"])
            .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
            .AllowCredentials()
            .AllowAnyHeader()));
builder.Services.AddCors(options =>
    options.AddPolicy("Development", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()));

// ── ForwardedHeaders (behind Nginx) ──
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ── YARP Reverse Proxy ──
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddGatewayTransforms();

// ── Health Checks ──
builder.Services.AddGatewayHealthChecks();

// ── OpenTelemetry (Tracing + Metrics + Prometheus) ──
var otelSettings = builder.Configuration.GetSection("OpenTelemetry");
var otlpEndpoint = otelSettings["OtlpEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: otelSettings["ServiceName"] ?? "sensorx-gateway",
            serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName.ToLower()
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
                // bỏ qua scraping endpoint của Prometheus khỏi traces
                opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics");
            })
            .AddHttpClientInstrumentation(opts => opts.RecordException = true);

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

// ── Controllers ──
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "v1";
    config.Title = "SensorX Gateway API";
    config.Version = "v1";
    config.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Nhập JWT token (không cần prefix 'Bearer ')"
    });
    config.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════
//  Middleware Pipeline 
// ═══════════════════════════════════════════════════════════════

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();


    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create database schema. Ensure the database is running.");
    }
}


app.UseForwardedHeaders();          // [1] Resolve real IP from Nginx
app.UseExceptionHandling();         // [2] Catch all exceptions → standard error
app.UseSecurityHeaders();           // [3] HSTS, CSP, X-Frame-Options
app.UseCors("Development");          // [4] CORS check before auth
app.UseAuthentication();            // [5] Validate JWT signature + claims
app.UseAuthorization();             // [6] Check role/scope → 403 if denied
app.UseCorrelationId();             // [7] Inject X-Correlation-Id


// ── Endpoints ──
app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics"); // [10] Prometheus scrape
app.MapReverseProxy();              // [9] YARP forward request



app.Run();
