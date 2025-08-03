using EasyVoice.Core.Interfaces.Tts;

namespace EasyVoice.Infrastructure.Tts.Engines;

public class KokoroTtsEngine : ITtsEngine
{
    public string Name => "kokoro";

    public KokoroTtsEngine()
    {
    }

    public async Task<TtsEngineResult> SynthesizeAsync(TtsEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        // Placeholder for Kokoro TTS API call.
        // This would involve making an HTTP request to the Kokoro TTS service endpoint.

        throw new NotImplementedException("Kokoro TTS synthesis is not implemented yet.");
    }

    public async Task<Stream> SynthesizeStreamAsync(TtsEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Kokoro TTS synthesis is not implemented yet.");
    }
}