using SensorX.Gateway.Domain.Primitives;

namespace SensorX.Gateway.Domain.Entities;

public class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHmac { get; private set; } = null!;
    public bool IsRevoked { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public Account Account { get; private set; } = null!;

    private RefreshToken() { }

    private RefreshToken(Guid userId, string tokenHmac, DateTimeOffset expiresAt) : base(Guid.NewGuid())
    {
        UserId = userId;
        TokenHmac = tokenHmac;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static RefreshToken Create(Guid userId, string tokenHmac, DateTimeOffset expiresAt)
    {
        return new RefreshToken(userId, tokenHmac, expiresAt);
    }

    public void Revoke()
    {
        IsRevoked = true;
    }

    public void MarkAsUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        IsRevoked = true; // Use it once
    }

    public bool IsValid()
    {
        return !IsRevoked && ExpiresAt >= DateTimeOffset.UtcNow;
    }
}
