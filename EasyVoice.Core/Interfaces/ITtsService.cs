using EasyVoice.Core.Models;
using System.Threading.Tasks;

namespace EasyVoice.Core.Interfaces;

/// <summary>
/// Defines the contract for the Text-to-Speech (TTS) generation service.
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// Generates TTS audio and subtitles based on the provided request.
    /// This method corresponds to the `generateTTS` function in the original Node.js project.
    /// </summary>
    /// <param name="request">The TTS request parameters for a standard generation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the TTS result with audio and SRT URLs.</returns>
    Task<TtsResult> GenerateTtsAsync(EdgeTtsRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates TTS audio as a streaming response for real-time playback.
    /// </summary>
    /// <param name="request">The TTS request parameters for streaming generation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an audio stream.</returns>
    Task<Stream> GenerateTtsStreamAsync(EdgeTtsRequest request, CancellationToken cancellationToken = default);
}
