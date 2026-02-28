using Microsoft.AspNetCore.Authorization;

namespace SensorX.Gateway.Api.Authorization;

/// <summary>
/// Attribute to decorate controllers/actions with permission requirements.
/// Usage: [Permission("perm1", "perm2")] — user needs ANY of the listed permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : AuthorizeAttribute
{
    public PermissionAttribute(params string[] permissions)
        : base($"Permission:{string.Join(",", permissions)}")
    {
    }
}
