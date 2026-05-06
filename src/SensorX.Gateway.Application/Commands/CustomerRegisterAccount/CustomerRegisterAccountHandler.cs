using MassTransit;
using MediatR;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Commands.CustomerRegisterAccount;

public sealed class CustomerRegisterAccountHandler(
    IAccountRepository _accountRepository,
    IPasswordHasher _passwordHasher,
    IUnitOfWork _unitOfWork,
    IPublishEndpoint _publishEndpoint
) : IRequestHandler<CustomerRegisterAccountCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(CustomerRegisterAccountCommand request, CancellationToken cancellationToken)
    {
        if (await _accountRepository.AnyByEmailAsync(request.Email))
            return ApiResponse<object>.FailResponse("Email already registered");

        var passwordHash = await _passwordHasher.HashAsync(request.Password);

        var account = Account.Create(request.Email, request.Name, passwordHash, Role.Customer);

        _accountRepository.Add(account);

        // Bắn sự kiện ra RabbitMQ
        await _publishEndpoint.Publish(new CustomerRegisterAccountEvent
        {
            AccountId = account.Id,
            Email = account.Email,
            Name = request.Name,
            Phone = request.Phone,
            TaxCode = request.TaxCode,
            Address = request.Address
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { userId = account.Id }, "Account registered");
    }
}