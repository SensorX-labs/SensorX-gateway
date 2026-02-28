using System.Text.Json;

namespace SensorX.Gateway.Domain.Entities;

public class IdempotencyKeyEntry
{
    public string Key { get; set; } = null!;
    public JsonDocument Response { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
