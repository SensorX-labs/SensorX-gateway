namespace SensorX.Gateway.Domain.Interfaces;

public interface IPasswordHasher
{
    Task<string> HashAsync(string password);
    Task<bool> VerifyAsync(string password, string storedHash);
}
