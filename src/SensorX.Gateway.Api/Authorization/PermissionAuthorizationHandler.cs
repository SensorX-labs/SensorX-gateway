using Microsoft.AspNetCore.Authorization;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRedisPermissionService _permissionService;

    public PermissionAuthorizationHandler(IRedisPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst("sub")?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return;

        var permissions = await _permissionService.GetPermissionsAsync(userId);

        if (permissions.Contains("full_permission") ||
            requirement.Permissions.Any(p => permissions.Contains(p)))
        {
            context.Succeed(requirement);
        }
    }
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public string[] Permissions { get; }
    public PermissionRequirement(params string[] permissions) => Permissions = permissions;
}
