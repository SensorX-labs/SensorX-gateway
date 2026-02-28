namespace SensorX.Gateway.Application.Interfaces;

public interface IAccessTokenService
{
    string CreateToken(Guid userId, string email, string role, string scope);
    string CreateMfaToken(Guid userId);
}
