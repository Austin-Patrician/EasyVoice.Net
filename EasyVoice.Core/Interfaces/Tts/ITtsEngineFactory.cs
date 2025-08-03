namespace EasyVoice.Core.Interfaces.Tts;

/// <summary>
/// Defines the contract for a factory that provides TTS engine instances.
/// </summary>
public interface ITtsEngineFactory
{
    /// <summary>
    /// Gets a TTS engine by its name.
    /// </summary>
    /// <param name="name">The name of the engine (e.g., "edge", "openai").</param>
    /// <returns>An instance of the requested TTS engine.</returns>
    /// <exception cref="NotSupportedException">Thrown when no engine with the specified name is registered.</exception>
    ITtsEngine GetEngine(string name);
}
