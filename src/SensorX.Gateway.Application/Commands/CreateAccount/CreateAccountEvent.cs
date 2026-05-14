
using MassTransit;
using SensorX.Gateway.Domain.Enums;

namespace SensorX.Gateway.Application.Commands.CreateAccount;

[MessageUrn("account-created")]
public sealed record CreateAccountEvent
{
    public Guid AccountId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public Role Role { get; init; }
    public Guid? WarehouseId { get; init; }
    public DateTimeOffset RegisteredAt { get; init; }
}
