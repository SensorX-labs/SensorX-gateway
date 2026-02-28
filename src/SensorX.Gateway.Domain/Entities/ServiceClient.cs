namespace SensorX.Gateway.Domain.Entities;

public class ServiceClient
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = null!;
    public string ClientSecretHash { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
