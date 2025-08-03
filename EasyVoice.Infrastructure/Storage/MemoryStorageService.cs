using EasyVoice.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace EasyVoice.Infrastructure.Storage;

/// <summary>
/// An in-memory implementation of the storage service.
/// </summary>
public class MemoryStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public Task<bool> SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var serializedValue = JsonSerializer.Serialize(value);
        _cache[key] = serializedValue;
        return Task.FromResult(true);
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (_cache.TryGetValue(key, out var serializedValue))
        {
            var value = JsonSerializer.Deserialize<T>(serializedValue);
            return Task.FromResult<T?>(value);
        }
        return Task.FromResult<T?>(null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task CleanExpiredAsync(CancellationToken cancellationToken = default)
    {
        // In this simple memory storage, expiration is expected to be handled
        // by the consumer (e.g., CacheService) before calling get/set.
        // The original Node.js implementation iterates through items and checks
        // an 'expireAt' property. A more robust .NET implementation would use
        // IMemoryCache which handles expiration automatically. This implementation
        // is a direct port of the simple Map-based storage.
        // For now, this method will be a no-op, as the CacheService will wrap this logic.
        return Task.CompletedTask;
    }
}
