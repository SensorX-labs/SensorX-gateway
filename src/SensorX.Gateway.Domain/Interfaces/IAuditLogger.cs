using System.Text.Json;

namespace SensorX.Gateway.Domain.Interfaces;

public interface IAuditLogger
{
    ValueTask WriteAsync(AuditEntry entry);
}

public class AuditEntry
{
    public string EventType { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? ClientIp { get; set; }
    public string? Endpoint { get; set; }
    public string? Method { get; set; }
    public int? StatusCode { get; set; }
    public int? DurationMs { get; set; }
    public JsonDocument? EventData { get; set; }
}
