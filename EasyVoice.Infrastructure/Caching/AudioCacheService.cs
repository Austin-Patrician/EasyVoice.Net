using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasyVoice.Infrastructure.Caching;

file class CacheItem<T>
{
    public T Value { get; set; } = default!;
    public long ExpireAt { get; set; }
}

public class AudioCacheOptions
{
    public long Ttl { get; set; } = 365L * 24 * 60 * 60 * 1000; // 1 year in milliseconds
}

/// <summary>
/// Implements the service for caching audio generation results.
/// </summary>
public class AudioCacheService : IAudioCacheService
{
    private readonly IStorageService _storage;
    private readonly AudioCacheOptions _options;

    public AudioCacheService(IStorageService storage, AudioCacheOptions options)
    {
        _storage = storage;
        _options = options;
    }

    private static string GenerateKey(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<AudioCacheData?> GetAudioAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateKey(key);
        var item = await _storage.GetAsync<CacheItem<AudioCacheData>>(cacheKey, cancellationToken);

        if (item == null)
        {
            return null;
        }

        if (item.ExpireAt < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            await _storage.DeleteAsync(cacheKey, cancellationToken);
            return null;
        }

        return item.Value;
    }

    public async Task<bool> HasAudioAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateKey(key);
        var item = await _storage.GetAsync<CacheItem<AudioCacheData>>(cacheKey, cancellationToken);
        return item != null && item.ExpireAt >= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public Task<bool> SetAudioAsync(string key, AudioCacheData audioData, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateKey(key);
        var item = new CacheItem<AudioCacheData>
        {
            Value = audioData,
            ExpireAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _options.Ttl
        };
        return _storage.SetAsync(cacheKey, item, cancellationToken);
    }

    public Task CleanExpiredAsync(CancellationToken cancellationToken = default)
    {
        // The clean expired logic is now part of the storage services,
        // especially for FileStorage. Get/Has methods also perform on-demand cleaning.
        return _storage.CleanExpiredAsync(cancellationToken);
    }
}
