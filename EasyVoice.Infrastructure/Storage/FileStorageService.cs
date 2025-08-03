using EasyVoice.Core.Interfaces;
using System.Text.Json;

namespace EasyVoice.Infrastructure.Storage;

public class FileStorageOptions
{
    public string CacheDir { get; set; } = "Cache";
}

/// <summary>
/// A file-based implementation of the storage service.
/// </summary>
public class FileStorageService : IStorageService
{
    private readonly string _cacheDir;

    public FileStorageService(FileStorageOptions options)
    {
        _cacheDir = options.CacheDir;
        Directory.CreateDirectory(_cacheDir);
    }

    private string GetFilePath(string key)
    {
        // It's important to sanitize the key to prevent directory traversal attacks.
        // For this implementation, we'll assume the key is a safe hash.
        // A more robust solution would involve hashing the key or sanitizing it.
        return Path.Combine(_cacheDir, $"{key}.json");
    }

    public async Task<bool> SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);
        var serializedValue = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, serializedValue, cancellationToken);
        return true;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var filePath = GetFilePath(key);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var serializedValue = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<T>(serializedValue);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    public async Task CleanExpiredAsync(CancellationToken cancellationToken = default)
    {
        // This logic relies on the cached item having an 'expireAt' property,
        // which is not enforced by the generic T. The CacheService wrapper will handle this.
        // For now, we are porting the logic as-is.
        var files = Directory.GetFiles(_cacheDir, "*.json");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var item = JsonSerializer.Deserialize<JsonElement>(content);
                if (item.TryGetProperty("expireAt", out var expireAtElement) && expireAtElement.TryGetInt64(out var expireAt))
                {
                    if (expireAt < now)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Ignore files that can't be parsed or don't have the property.
            }
        }
    }
}
