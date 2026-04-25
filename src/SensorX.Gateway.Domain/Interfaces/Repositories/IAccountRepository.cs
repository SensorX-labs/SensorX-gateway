using SensorX.Gateway.Domain.Entities;

namespace SensorX.Gateway.Domain.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByEmailAsync(string email);
    Task<Account?> GetByIdAsync(Guid id);
    Task<bool> AnyByEmailAsync(string email);
    Task<IEnumerable<Account>> GetAllAsync();
    void Add(Account account);
}
