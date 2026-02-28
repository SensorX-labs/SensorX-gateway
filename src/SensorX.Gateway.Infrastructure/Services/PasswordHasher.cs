using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public async Task<string> HashAsync(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            MemorySize = 65536,  // 64MB
            Iterations = 3
        };
        var hash = await argon2.GetBytesAsync(32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public async Task<bool> VerifyAsync(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        var computedHash = await argon2.GetBytesAsync(32);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }
}
