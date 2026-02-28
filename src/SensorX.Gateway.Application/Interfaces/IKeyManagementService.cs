using Microsoft.IdentityModel.Tokens;

namespace SensorX.Gateway.Application.Interfaces;

public interface IKeyManagementService
{
    SigningCredentials GetSigningCredentials();
    string GetKid();
    object GetJwksResponse();
    IEnumerable<SecurityKey> ResolveSigningKey(
        string token, SecurityToken securityToken, string kid,
        TokenValidationParameters validationParameters);
    string GetPublicKeyPem();
}
