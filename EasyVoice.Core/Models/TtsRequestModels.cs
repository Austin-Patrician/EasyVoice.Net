using System.ComponentModel.DataAnnotations;

namespace EasyVoice.Core.Models;

/// <summary>
/// Represents the request for standard Edge TTS generation.
/// Corresponds to the 'edgeSchema' in the original Node.js project.
/// </summary>
public record EdgeTtsRequest(
    [Required(ErrorMessage = "文本最少 5 字符！")]
    [MinLength(5, ErrorMessage = "文本最少 5 字符！")]
    string Text,
    [Required] [MinLength(1)] string Voice,
    string? Pitch,
    string? Volume,
    string? Rate
);