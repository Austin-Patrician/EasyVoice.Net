namespace EasyVoice.Core.Interfaces;

/// <summary>
/// Defines the contract for a generic key-value storage service.
/// This corresponds to the 'BaseStorage' abstract class in the original Node.js project.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Sets a value in the storage with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true if the operation was successful.</returns>
    Task<bool> SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from the storage by its key.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value, or null if the key is not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Deletes a value from the storage by its key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up any expired entries in the storage.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CleanExpiredAsync(CancellationToken cancellationToken = default);
}
