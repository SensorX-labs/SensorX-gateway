namespace SensorX.Gateway.Application.Commands.CustomerRegisterAccount;

using MediatR;
using SensorX.Gateway.Application.Commons.Responses;

public sealed record CustomerRegisterAccountCommand(
    string Email,
    string Password,
    string Name,
    string? Phone,
    string TaxCode,
    string? Address
) : IRequest<ApiResponse<object>>;