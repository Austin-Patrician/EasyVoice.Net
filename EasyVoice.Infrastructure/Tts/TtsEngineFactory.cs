using EasyVoice.Core.Interfaces.Tts;

namespace EasyVoice.Infrastructure.Tts;

/// <summary>
/// Provides instances of TTS engines.
/// </summary>
public class TtsEngineFactory : ITtsEngineFactory
{
    private readonly IReadOnlyDictionary<string, ITtsEngine> _engines;

    public TtsEngineFactory(IEnumerable<ITtsEngine> engines)
    {
        _engines = engines.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ITtsEngine GetEngine(string name)
    {
        if (_engines.TryGetValue(name, out var engine))
        {
            return engine;
        }

        throw new NotSupportedException($"TTS engine '{name}' is not registered or supported.");
    }
}
