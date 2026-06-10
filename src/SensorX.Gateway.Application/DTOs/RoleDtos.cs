using SensorX.Gateway.Domain.Enums;

namespace SensorX.Gateway.Application.DTOs;

// ── Role Responses ──
public record RoleResponse(int Id, string Name);
public record UserRoleAssignmentResponse(Guid UserId, Role Role, string RoleName);