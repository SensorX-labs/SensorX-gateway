using SensorX.Gateway.Domain.Entities;

namespace SensorX.Gateway.Domain.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id);
    Task<Role?> GetByNameAsync(string name);
    Task<IEnumerable<Role>> GetAllAsync();
    Task<IEnumerable<Role>> GetUserRolesAsync(Guid userId);
    Task<bool> ExistsAsync(Guid id);
    void Add(Role role);
    void Remove(Role role);
}