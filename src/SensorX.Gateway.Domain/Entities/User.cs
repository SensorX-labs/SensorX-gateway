using SensorX.Gateway.Domain.Primitives;

namespace SensorX.Gateway.Domain.Entities;

public class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();
    public bool IsLocked { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public int LoginFailCount { get; private set; }
    public int LockCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation
    private readonly List<UserRole> _userRoles = new();
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    // EF Core requires a parameterless constructor
    private User()
    {
    }

    private User(string email, string passwordHash) : base(Guid.NewGuid())
    {
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static User Create(string email, string passwordHash)
    {
        return new User(email, passwordHash);
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

    public void AddRole(Guid roleId)
    {
        if (!_userRoles.Any(ur => ur.RoleId == roleId))
        {
            _userRoles.Add(new UserRole { UserId = Id, RoleId = roleId });
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public bool RemoveRole(Guid roleId)
    {
        var userRole = _userRoles.FirstOrDefault(ur => ur.RoleId == roleId);
        if (userRole != null)
        {
            _userRoles.Remove(userRole);
            UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
        return false;
    }

    public bool HasRole(Guid roleId)
    {
        return _userRoles.Any(ur => ur.RoleId == roleId);
    }

    public IEnumerable<Guid> GetRoleIds()
    {
        return _userRoles.Select(ur => ur.RoleId).ToList();
    }

    public void ClearRoles()
    {
        if (_userRoles.Any())
        {
            _userRoles.Clear();
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void RemoveAllRefreshTokens()
    {
        _refreshTokens.Clear();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
