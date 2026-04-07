using Microsoft.EntityFrameworkCore;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(Guid id)
    {
        return await _context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Role?> GetByNameAsync(string name)
    {
        return await _context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Name == name);
    }

    public async Task<IEnumerable<Role>> GetAllAsync()
    {
        return await _context.Roles
            .Include(r => r.UserRoles)
            .ToListAsync();
    }

    public async Task<IEnumerable<Role>> GetUserRolesAsync(Guid userId)
    {
        return await _context.Roles
            .Where(r => r.UserRoles.Any(ur => ur.UserId == userId))
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Roles.AnyAsync(r => r.Id == id);
    }

    public void Add(Role role)
    {
        _context.Roles.Add(role);
    }

    public void Remove(Role role)
    {
        _context.Roles.Remove(role);
    }
}