using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SensorX.Gateway.Application.Interfaces;

namespace SensorX.Gateway.Infrastructure.Services;

public class KeyManagementService : IKeyManagementService
{
    private readonly RSA _privateKey;
    private readonly RsaSecurityKey _signingKey;
    private readonly string _kid;

    public KeyManagementService(IConfiguration configuration)
    {
        var keyPath = configuration["JwtSettings:PrivateKeyPath"] ?? "Keys/private.key";
        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(File.ReadAllText(keyPath));
        _kid = configuration["JwtSettings:Kid"] ?? $"key-{DateTime.UtcNow:yyyy-MM}";
        _signingKey = new RsaSecurityKey(_privateKey) { KeyId = _kid };
    }

    public SigningCredentials GetSigningCredentials()
        => new(_signingKey, SecurityAlgorithms.RsaSha256);

    public string GetKid() => _kid;

    public object GetJwksResponse()
    {
        var parameters = _privateKey.ExportParameters(false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA", use = "sig", kid = _kid, alg = "RS256",
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!)
                }
            }
        };
    }

    public IEnumerable<SecurityKey> ResolveSigningKey(
        string token, SecurityToken securityToken, string kid,
        TokenValidationParameters validationParameters)
    {
        yield return _signingKey;
    }

    public string GetPublicKeyPem() => _privateKey.ExportRSAPublicKeyPem();
}
