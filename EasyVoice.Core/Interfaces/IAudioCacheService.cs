using EasyVoice.Core.Models;

namespace EasyVoice.Core.Interfaces;

/// <summary>
/// Defines the contract for a service that caches audio generation results.
/// </summary>
public interface IAudioCacheService
{
    /// <summary>
    /// Caches the audio data for a given key.
    /// </summary>
    /// <param name="key">The cache key, typically a hash of the request parameters.</param>
    /// <param name="audioData">The audio data to cache.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if the operation was successful.</returns>
    Task<bool> SetAudioAsync(string key, AudioCacheData audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves cached audio data by its key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cached data, or null if not found.</returns>
    Task<AudioCacheData?> GetAudioAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if audio data exists in the cache for a given key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if the key exists, otherwise false.</returns>
    Task<bool> HasAudioAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up any expired entries in the cache.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CleanExpiredAsync(CancellationToken cancellationToken = default);
}
