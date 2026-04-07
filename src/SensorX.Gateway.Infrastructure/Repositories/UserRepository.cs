using Microsoft.EntityFrameworkCore;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> AnyByEmailAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }
}
