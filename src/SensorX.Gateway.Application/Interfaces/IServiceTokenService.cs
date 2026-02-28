namespace SensorX.Gateway.Application.Interfaces;

public interface IServiceTokenService
{
    Task<string?> AuthenticateClient(string clientId, string clientSecret);
}
