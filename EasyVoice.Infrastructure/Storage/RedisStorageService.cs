using EasyVoice.Core.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace EasyVoice.Infrastructure.Storage;

public class RedisStorageOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
}

/// <summary>
/// A Redis-based implementation of the storage service.
/// </summary>
public class RedisStorageService : IStorageService, IAsyncDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;

    public RedisStorageService(RedisStorageOptions options)
    {
        _redis = ConnectionMultiplexer.Connect(options.ConnectionString);
        _database = _redis.GetDatabase();
    }

    public async Task<bool> SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var serializedValue = JsonSerializer.Serialize(value);
        
        // The original implementation calculates TTL based on an 'expireAt' property.
        // We will replicate this logic, assuming the CacheService wrapper provides it.
        TimeSpan? expiry = null;
        var jsonElement = JsonSerializer.SerializeToElement(value);
        if (jsonElement.TryGetProperty("expireAt", out var expireAtElement) && expireAtElement.TryGetInt64(out var expireAt))
        {
            var expireAtDate = DateTimeOffset.FromUnixTimeMilliseconds(expireAt);
            var ttl = expireAtDate - DateTimeOffset.UtcNow;
            if (ttl > TimeSpan.Zero)
            {
                expiry = ttl;
            }
        }

        return await _database.StringSetAsync(key, serializedValue, expiry);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var redisValue = await _database.StringGetAsync(key);
        if (redisValue.IsNullOrEmpty)
        {
            return null;
        }
        return JsonSerializer.Deserialize<T>(redisValue.ToString());
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(key);
    }

    public Task CleanExpiredAsync(CancellationToken cancellationToken = default)
    {
        // Redis handles expiration automatically, so this method is a no-op.
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _redis.DisposeAsync();
    }
}
