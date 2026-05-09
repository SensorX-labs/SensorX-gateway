using MassTransit;

namespace SensorX.Gateway.Application.Commands.CustomerRegisterAccount;

[MessageUrn("customer-registered")]
public sealed record CustomerRegisterAccountEvent
{
    public Guid AccountId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; } = string.Empty;
    public string TaxCode { get; init; } = string.Empty;
    public string? Address { get; init; } = string.Empty;
}