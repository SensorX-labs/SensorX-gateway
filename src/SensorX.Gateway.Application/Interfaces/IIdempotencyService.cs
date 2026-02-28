namespace SensorX.Gateway.Application.Interfaces;

public interface IIdempotencyService
{
    Task<object?> GetCachedResponseAsync(string key);
    Task StoreAsync(string key, object response);
}
