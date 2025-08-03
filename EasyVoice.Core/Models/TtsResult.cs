namespace EasyVoice.Core.Models;

/// <summary>
/// Represents the result of a TTS generation task.
/// </summary>
public record TtsResult(
    string AudioUrl,
    string SrtUrl,
    bool IsPartial = false
);