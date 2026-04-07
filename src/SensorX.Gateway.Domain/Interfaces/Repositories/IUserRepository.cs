using SensorX.Gateway.Domain.Entities;

namespace SensorX.Gateway.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task<bool> AnyByEmailAsync(string email);
    void Add(User user);
}
