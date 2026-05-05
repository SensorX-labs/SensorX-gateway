namespace SensorX.Gateway.Application.Commands.CreateAccount;

using System.Text.Json;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using Role = SensorX.Gateway.Domain.Enums.Role;

public class CreateAccountHandler(
    IAccountRepository _accountRepository,
    IUnitOfWork _unitOfWork,
    IPasswordHasher _passwordHasher,
    IPublishEndpoint _publishEndpoint
) : IRequestHandler<CreateAccountCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (await _accountRepository.AnyByEmailAsync(request.Email))
            return ApiResponse<object>.FailResponse("Email already exists");

        var passwordHash = await _passwordHasher.HashAsync(request.Password);

        // Split email for a preliminary FullName
        var generatedFullName = request.Email.Split('@')[0];

        if (request.Role == Role.Customer)
            return ApiResponse<object>.FailResponse("Customer role is not allowed for staff creation");

        if (request.Role == Role.Admin)
            return ApiResponse<object>.FailResponse("Admin role is not allowed for staff creation");

        var account = Account.Create(request.Email, generatedFullName, passwordHash, request.Role);

        _accountRepository.Add(account);

        // Bắn sự kiện ra RabbitMQ
        await _publishEndpoint.Publish(new CreateAccountEvent
        {
            AccountId = account.Id,
            Email = account.Email,
            FullName = account.FullName,
            Role = account.Role,
            RegisteredAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { userId = account.Id }, "Account created");
    }
}