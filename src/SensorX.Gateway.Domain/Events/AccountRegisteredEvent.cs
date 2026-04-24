
namespace SensorX.Gateway.Domain.Events;

public record AccountRegisteredEvent
{
    public Guid AccountId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; init; }
}
