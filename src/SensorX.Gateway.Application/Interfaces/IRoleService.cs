using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;

namespace SensorX.Gateway.Application.Interfaces;

public interface IRoleService
{
    Task<ApiResponse<IEnumerable<RoleResponse>>> GetAllRolesAsync();
    Task<ApiResponse<RoleResponse>> GetRoleByIdAsync(Guid id);
    Task<ApiResponse<IEnumerable<RoleResponse>>> GetUserRolesAsync(Guid userId);
    Task<ApiResponse<RoleResponse>> CreateRoleAsync(CreateRoleRequest request);
    Task<ApiResponse<RoleResponse>> UpdateRoleAsync(Guid id, UpdateRoleRequest request);
    Task<ApiResponse> DeleteRoleAsync(Guid id);
    Task<ApiResponse> AssignRoleToUserAsync(AssignRoleRequest request);
    Task<ApiResponse> RemoveRoleFromUserAsync(Guid userId, Guid roleId);
}