namespace SensorX.Gateway.Domain.Interfaces;

public interface IRedisPermissionService
{
    Task SetPermissionsAsync(Guid userId, IReadOnlyList<string> permissions);
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId);
    Task RemovePermissionsAsync(Guid userId);
}
