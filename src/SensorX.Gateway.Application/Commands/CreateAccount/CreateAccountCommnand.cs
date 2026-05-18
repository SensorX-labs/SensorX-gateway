using MediatR;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Domain.Enums;

namespace SensorX.Gateway.Application.Commands.CreateAccount;

public sealed record CreateAccountCommand(string Email, string Password, Role Role, Guid? WarehouseId = null) : IRequest<ApiResponse<object>>;
