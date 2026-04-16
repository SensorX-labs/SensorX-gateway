using SensorX.Gateway.Application.Commons.Responses;
using SensorX.Gateway.Application.DTOs;

namespace SensorX.Gateway.Application.Interfaces;

public interface IRoleService
{
    ApiResponse<IEnumerable<RoleResponse>> GetAllRoles();
    Task<ApiResponse<RoleResponse>> GetUserRoleAsync(Guid userId);
    Task<ApiResponse> AssignRoleToUserAsync(AssignRoleRequest request);
}