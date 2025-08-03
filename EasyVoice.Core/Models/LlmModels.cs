namespace EasyVoice.Core.Models;

/// <summary>
/// LLM 提供商类型枚举
/// </summary>
public enum LlmProviderType
{
    OpenAI,
    Doubao
}

/// <summary>
/// LLM 提供商类型枚举
/// </summary>
public enum OpenAiVoiceType
{
    Alloy,
    Ash,
    Ballad,
    Coral,
    Echo,
    Fable,
    Onyx,
    Nova,
    Sage,
    Shimmer,
    Verse
}

public static class OpenAiVoiceTypeExtensions
{
    public static string GetName(this OpenAiVoiceType voiceType)
    {
        return voiceType switch
        {
            OpenAiVoiceType.Alloy => "alloy",
            OpenAiVoiceType.Ash => "ash",
            OpenAiVoiceType.Ballad => "ballad",
            OpenAiVoiceType.Coral => "coral",
            OpenAiVoiceType.Echo => "echo",
            OpenAiVoiceType.Fable => "fable",
            OpenAiVoiceType.Onyx => "onyx",
            OpenAiVoiceType.Nova => "nova",
            OpenAiVoiceType.Sage => "sage",
            OpenAiVoiceType.Shimmer => "shimmer",
            OpenAiVoiceType.Verse => "verse",
            _ => "unknown"
        };
    }
}


/// <summary>
/// LLM 提供商信息
/// </summary>
public class LlmProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public LlmProviderType Type { get; set; }
    public bool IsAvailable { get; set; }
    public string Description { get; set; } = string.Empty;
    public string[] SupportedLanguages { get; set; } = Array.Empty<string>();
    public string[] Capabilities { get; set; } = Array.Empty<string>();
}
