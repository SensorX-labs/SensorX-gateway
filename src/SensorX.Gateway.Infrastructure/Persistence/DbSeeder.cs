using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Infrastructure.Persistence;

public class DbSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DbSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedAdminAsync();
    }

    private async Task SeedAdminAsync()
    {
        var adminEmail = _configuration["DefaultAdmin:Email"] ?? "admin@sensorx.com";
        var adminPassword = _configuration["DefaultAdmin:Password"] ?? "admin";
        var adminFullName = _configuration["DefaultAdmin:FullName"] ?? "System Administrator";

        var adminAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == adminEmail);

        if (adminAccount == null)
        {
            _logger.LogInformation("Creating default admin account: {Email}", adminEmail);
            var passwordHash = await _passwordHasher.HashAsync(adminPassword);
            adminAccount = Account.Create(adminEmail, adminFullName, passwordHash, Role.Admin);
            _context.Accounts.Add(adminAccount);
        }
        else
        {
            // Verify if password in config matches the one in DB
            var isPasswordMatch = await _passwordHasher.VerifyAsync(adminPassword, adminAccount.PasswordHash);
            if (!isPasswordMatch)
            {
                _logger.LogInformation("Updating password for admin account: {Email} (mismatch with config)", adminEmail);
                var newPasswordHash = await _passwordHasher.HashAsync(adminPassword);
                adminAccount.ChangePassword(newPasswordHash);
                _context.Accounts.Update(adminAccount);
            }
            else
            {
                _logger.LogInformation("Admin account already exists and password matches config.");
                return;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Admin account processed successfully.");
    }
}
