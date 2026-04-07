namespace SensorX.Gateway.Domain.Interfaces;

public interface IAccessTokenService
{
    string CreateToken(Guid userId, string email, string role, string scope);
}
