namespace SensorX.Gateway.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public bool IsLocked { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int LoginFailCount { get; set; }
    public int LockCount { get; set; }

    // MFA (Phase 2)
    public string? TotpSecret { get; set; }
    public bool MfaEnabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
