namespace SensorX.Gateway.Domain.Primitives;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}
