using Microsoft.EntityFrameworkCore;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Account?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();
        return await _context.Accounts
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task<Account?> GetByIdAsync(Guid id)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> AnyByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();
        return await _context.Accounts.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task<IEnumerable<Account>> GetAllAsync()
    {
        return await _context.Accounts
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Account> Items, int TotalCount)> GetPagedAsync(
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
    )
    {
        var query = _context.Accounts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearchTerm = searchTerm.Trim().ToLower();
            query = query.Where(account =>
                account.Email.ToLower().Contains(normalizedSearchTerm) ||
                account.FullName.ToLower().Contains(normalizedSearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLower();
            query = query.Where(account => account.Email.ToLower().Contains(normalizedEmail));
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var normalizedFullName = fullName.Trim().ToLower();
            query = query.Where(account => account.FullName.ToLower().Contains(normalizedFullName));
        }

        if (role.HasValue)
        {
            query = query.Where(account => account.Role == role.Value);
        }

        if (isLocked.HasValue)
        {
            query = query.Where(account => account.IsLocked == isLocked.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(account => account.WarehouseId == warehouseId.Value);
        }

        if (createdFrom.HasValue)
        {
            query = query.Where(account => account.CreatedAt >= createdFrom.Value);
        }

        if (createdTo.HasValue)
        {
            query = query.Where(account => account.CreatedAt <= createdTo.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(account => account.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public void Add(Account account)
    {
        _context.Accounts.Add(account);
    }
}
