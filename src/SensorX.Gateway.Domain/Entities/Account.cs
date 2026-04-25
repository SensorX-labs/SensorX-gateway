using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Primitives;

namespace SensorX.Gateway.Domain.Entities;

public class Account : AggregateRoot<Guid>
{
    public string Email { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string? AvatarUrl { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();
    public bool IsLocked { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public int LoginFailCount { get; private set; }
    public int LockCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Role Role { get; private set; }

    // Navigation
    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // EF Core requires a parameterless constructor
    private Account()
    {
    }

    private Account(string email, string fullName, string passwordHash, Role role = Role.SaleStaff) : base(Guid.NewGuid())
    {
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Account Create(string email, string fullName, string passwordHash, Role role = Role.SaleStaff)
    {
        return new Account(email, fullName, passwordHash, role);
    }

    public void UpdateProfile(string fullName, string? avatarUrl)
    {
        FullName = fullName;
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SecurityStamp = Guid.NewGuid(); // invalidate old tokens
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordFailedLogin(int maxAttempts, int lockoutMinutes)
    {
        LoginFailCount++;
        if (LoginFailCount >= maxAttempts)
        {
            IsLocked = true;
            LockCount++;
            LockedUntil = DateTimeOffset.UtcNow.AddMinutes(lockoutMinutes);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetLoginFailures()
    {
        LoginFailCount = 0;
        IsLocked = false;
        LockedUntil = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetRole(Role role)
    {
        if (Role != role)
        {
            Role = role;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void ToggleLock()
    {
        IsLocked = !IsLocked;
        if (!IsLocked)
        {
            LockedUntil = null;
            LoginFailCount = 0;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveAllRefreshTokens()
    {
        _refreshTokens.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
