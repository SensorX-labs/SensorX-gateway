using MassTransit;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Application.Commands.CreateAccount;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Services;

public class RoleService : IRoleService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        ILogger<RoleService> logger)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public ApiResponse<IEnumerable<RoleResponse>> GetAllRoles()
    {
        var roles = Enum.GetValues<Role>()
            .Select(r => new RoleResponse((int)r, r.ToString()));
            
        return ApiResponse<IEnumerable<RoleResponse>>.SuccessResponse(roles);
    }

    public async Task<ApiResponse<RoleResponse>> GetUserRoleAsync(Guid userId)
    {
        var account = await _accountRepository.GetByIdAsync(userId);
        if (account == null)
            return ApiResponse<RoleResponse>.FailResponse("Account not found");

        var role = account.Role;
        return ApiResponse<RoleResponse>.SuccessResponse(new RoleResponse((int)role, role.ToString()));
    }
}
