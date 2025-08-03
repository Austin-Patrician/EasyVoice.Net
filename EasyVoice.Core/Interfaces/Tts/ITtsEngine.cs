namespace EasyVoice.Core.Interfaces.Tts;

public record TtsEngineRequest(
    string Text,
    string Voice,
    string? Rate,
    string? Pitch,
    string? Volume,
    string OutputPath // Base path for the output file, without extension
);

public record TtsEngineResult(
    string AudioFilePath,
    string SubtitleFilePath
);

/// <summary>
/// Defines the contract for a TTS engine client.
/// </summary>
public interface ITtsEngine
{
    /// <summary>
    /// Gets the unique name of the engine (e.g., "edge", "openai").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Synthesizes speech from the given request and returns the result.
    /// </summary>
    /// <param name="request">The TTS engine request containing text, voice, and output path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the paths to the generated audio and subtitle files.</returns>
    Task<TtsEngineResult> SynthesizeAsync(TtsEngineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesizes speech from the given request and returns a streaming audio response.
    /// </summary>
    /// <param name="request">The TTS engine request containing text, voice, and output path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an audio stream.</returns>
    Task<Stream> SynthesizeStreamAsync(TtsEngineRequest request, CancellationToken cancellationToken = default);
}
