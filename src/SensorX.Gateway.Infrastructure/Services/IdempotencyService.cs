using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Entities;
using SensorX.Gateway.Infrastructure.Persistence;

namespace SensorX.Gateway.Infrastructure.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly AppDbContext _db;

    public IdempotencyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object?> GetCachedResponseAsync(string key)
    {
        var record = await _db.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == key && k.ExpiresAt > DateTimeOffset.UtcNow);
        return record?.Response.Deserialize<object>();
    }

    public async Task StoreAsync(string key, object response)
    {
        _db.IdempotencyKeys.Add(new IdempotencyKeyEntry
        {
            Key = key,
            Response = JsonSerializer.SerializeToDocument(response),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync();
    }
}
