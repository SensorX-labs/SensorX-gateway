using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;

namespace SensorX.Gateway.Domain.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByEmailAsync(string email);
    Task<Account?> GetByIdAsync(Guid id);
    Task<bool> AnyByEmailAsync(string email);
    Task<IEnumerable<Account>> GetAllAsync();
    Task<(IReadOnlyList<Account> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        string? email,
        string? fullName,
        Role? role,
        bool? isLocked,
        Guid? warehouseId,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo
    );
    void Add(Account account);
}
