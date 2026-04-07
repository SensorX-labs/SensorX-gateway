namespace SensorX.Gateway.Application.DTOs;

// ── Role Requests ──
public record CreateRoleRequest(string Name);
public record UpdateRoleRequest(string Name);
public record AssignRoleRequest(Guid UserId, Guid RoleId);

// ── Role Responses ──
public record RoleResponse(Guid Id, string Name);
public record UserRoleAssignmentResponse(Guid UserId, Guid RoleId, string RoleName);