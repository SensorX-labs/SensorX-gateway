namespace SensorX.Gateway.Domain.Entities;

public class SigningKey
{
    public string Kid { get; set; } = null!;
    public string Algorithm { get; set; } = "RS256";
    public string PublicKey { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RetiredAt { get; set; }
}
