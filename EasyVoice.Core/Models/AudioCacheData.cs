namespace EasyVoice.Core.Models;

/// <summary>
/// Represents the data structure for a cached audio generation result.
/// Corresponds to the 'AudioData' interface in the original Node.js project.
/// </summary>
public record AudioCacheData
(
    string Voice,
    string Text,
    string Rate,
    string Pitch,
    string Volume,
    string AudioUrl,
    string SrtUrl
);
