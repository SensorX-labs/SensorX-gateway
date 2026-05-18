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

    public async Task<ApiResponse> AssignRoleToUserAsync(AssignRoleRequest request)
    {
        var account = await _accountRepository.GetByIdAsync(request.UserId);
        if (account == null)
            return ApiResponse.FailResponse("Account not found");

        if (!Enum.IsDefined(typeof(Role), request.Role))
            return ApiResponse.FailResponse("Invalid role");

        if (request.Role == Role.WarehouseStaff && !request.WarehouseId.HasValue)
            return ApiResponse.FailResponse("Vui lòng chọn kho bãi cho nhân viên kho");

        account.SetRole(request.Role, request.WarehouseId);

        // Bắn thông điệp đồng bộ sang dịch vụ Data
        await _publishEndpoint.Publish(new CreateAccountEvent
        {
            AccountId = account.Id,
            Email = account.Email,
            FullName = account.FullName,
            Role = account.Role,
            WarehouseId = account.WarehouseId,
            RegisteredAt = account.CreatedAt
        });

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Role {Role} assigned to account {AccountEmail} ({AccountId}) with WarehouseId {WarehouseId}", 
            request.Role, account.Email, account.Id, request.WarehouseId);

        return ApiResponse.SuccessResponse("Role assigned to user successfully");
    }
}