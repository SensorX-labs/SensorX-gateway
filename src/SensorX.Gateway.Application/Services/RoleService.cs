using Microsoft.Extensions.Logging;
using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<RoleService> logger)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<RoleResponse>>> GetAllRolesAsync()
    {
        var roles = await _roleRepository.GetAllAsync();
        var response = roles.Select(r => new RoleResponse(r.Id, r.Name));
        return ApiResponse<IEnumerable<RoleResponse>>.SuccessResponse(response);
    }

    public async Task<ApiResponse<RoleResponse>> GetRoleByIdAsync(Guid id)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null)
            return ApiResponse<RoleResponse>.FailResponse("Role not found");

        return ApiResponse<RoleResponse>.SuccessResponse(new RoleResponse(role.Id, role.Name));
    }

    public async Task<ApiResponse<IEnumerable<RoleResponse>>> GetUserRolesAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<IEnumerable<RoleResponse>>.FailResponse("User not found");

        var roles = await _roleRepository.GetUserRolesAsync(userId);
        var response = roles.Select(r => new RoleResponse(r.Id, r.Name));
        return ApiResponse<IEnumerable<RoleResponse>>.SuccessResponse(response);
    }

    public async Task<ApiResponse<RoleResponse>> CreateRoleAsync(CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<RoleResponse>.FailResponse("Role name is required");

        var existingRole = await _roleRepository.GetByNameAsync(request.Name);
        if (existingRole != null)
            return ApiResponse<RoleResponse>.FailResponse("Role already exists");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim().ToLower()
        };

        _roleRepository.Add(role);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Role created: {RoleName} ({RoleId})", role.Name, role.Id);

        return ApiResponse<RoleResponse>.SuccessResponse(new RoleResponse(role.Id, role.Name), "Role created successfully");
    }

    public async Task<ApiResponse<RoleResponse>> UpdateRoleAsync(Guid id, UpdateRoleRequest request)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null)
            return ApiResponse<RoleResponse>.FailResponse("Role not found");

        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<RoleResponse>.FailResponse("Role name is required");

        // Check if name already exists (excluding current role)
        var existingRole = await _roleRepository.GetByNameAsync(request.Name);
        if (existingRole != null && existingRole.Id != id)
            return ApiResponse<RoleResponse>.FailResponse("Role name already exists");

        var oldName = role.Name;
        role.Name = request.Name.Trim().ToLower();

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Role updated: {OldName} -> {NewName} ({RoleId})", oldName, role.Name, role.Id);

        return ApiResponse<RoleResponse>.SuccessResponse(new RoleResponse(role.Id, role.Name), "Role updated successfully");
    }

    public async Task<ApiResponse> DeleteRoleAsync(Guid id)
    {
        var role = await _roleRepository.GetByIdAsync(id);
        if (role == null)
            return ApiResponse.FailResponse("Role not found");

        // Prevent deleting roles that are assigned to users
        if (role.UserRoles != null && role.UserRoles.Any())
            return ApiResponse.FailResponse("Cannot delete role that is assigned to users. Remove role assignments first");

        _roleRepository.Remove(role);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Role deleted: {RoleName} ({RoleId})", role.Name, role.Id);

        return ApiResponse.SuccessResponse("Role deleted successfully");
    }

    public async Task<ApiResponse> AssignRoleToUserAsync(AssignRoleRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return ApiResponse.FailResponse("User not found");

        var role = await _roleRepository.GetByIdAsync(request.RoleId);
        if (role == null)
            return ApiResponse.FailResponse("Role not found");

        // Check if role is already assigned
        if (user.UserRoles.Any(ur => ur.RoleId == request.RoleId))
            return ApiResponse.FailResponse("Role is already assigned to user");

        user.AddRole(request.RoleId);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Role {RoleName} assigned to user {UserEmail} ({UserId})", 
            role.Name, user.Email, user.Id);

        return ApiResponse.SuccessResponse("Role assigned to user successfully");
    }

    public async Task<ApiResponse> RemoveRoleFromUserAsync(Guid userId, Guid roleId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse.FailResponse("User not found");

        var role = await _roleRepository.GetByIdAsync(roleId);
        if (role == null)
            return ApiResponse.FailResponse("Role not found");

        // Check if role is assigned
        if (!user.HasRole(roleId))
            return ApiResponse.FailResponse("Role is not assigned to user");

        var removed = user.RemoveRole(roleId);
        if (!removed)
            return ApiResponse.FailResponse("Failed to remove role from user");

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Role {RoleName} removed from user {UserEmail} ({UserId})",
            role.Name, user.Email, user.Id);

        return ApiResponse.SuccessResponse("Role removed from user successfully");
    }
}