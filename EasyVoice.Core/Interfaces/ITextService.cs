namespace EasyVoice.Core.Interfaces;

public record TextSplitResult(
    int Length,
    IReadOnlyList<string> Segments
);

/// <summary>
/// Defines the contract for text processing and splitting services.
/// </summary>
public interface ITextService
{
    /// <summary>
    /// Splits the input text into segments suitable for TTS processing.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="targetLength">The target length for each segment (default: 500).</param>
    /// <returns>A result containing the number of segments and the segments themselves.</returns>
    TextSplitResult SplitText(string text, int targetLength = 500);

}
