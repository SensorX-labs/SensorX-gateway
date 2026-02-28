using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Services;

public class AuditLogger : BackgroundService, IAuditLogger
{
    private readonly Channel<AuditEntry> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IServiceScopeFactory scopeFactory, ILogger<AuditLogger> logger)
    {
        _channel = Channel.CreateBounded<AuditEntry>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask WriteAsync(AuditEntry entry)
    {
        await _channel.Writer.WriteAsync(entry);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.AuditLog.Add(new AuditLogEntry
                {
                    EventType = entry.EventType,
                    UserId = entry.UserId,
                    CorrelationId = entry.CorrelationId,
                    ClientIp = entry.ClientIp,
                    Endpoint = entry.Endpoint,
                    Method = entry.Method,
                    StatusCode = entry.StatusCode,
                    DurationMs = entry.DurationMs,
                    EventData = entry.EventData
                });

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log entry");
            }
        }
    }
}
