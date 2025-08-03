namespace EasyVoice.Core.Constants;

/// <summary>
/// TTS 引擎类型枚举
/// 定义支持的文本转语音引擎类型
/// </summary>
public enum TtsEngineType
{
    /// <summary>
    /// Microsoft Edge TTS - 微软边缘浏览器的文本转语音引擎
    /// 特点：免费、多语言支持、音质良好
    /// </summary>
    Edge,

    /// <summary>
    /// OpenAI TTS - OpenAI 的文本转语音引擎
    /// 特点：AI 驱动、自然语音、支持多种语音风格
    /// </summary>
    OpenAi,

    /// <summary>
    /// 豆包 TTS - 字节跳动的文本转语音引擎
    /// 特点：中文优化、低延迟、高音质
    /// </summary>
    Doubao,

    /// <summary>
    /// Kokoro TTS - 专门用于日文语音合成的引擎
    /// 特点：日文专业、情感丰富
    /// </summary>
    Kokoro
}

/// <summary>
/// LLM 模型类型枚举
/// 定义支持的大语言模型类型
/// </summary>
public enum LlmModelType
{
    /// <summary>
    /// OpenAI GPT-4 模型
    /// 特点：强大的理解能力、多语言支持、高质量文本分析
    /// </summary>
    OpenAI,

    /// <summary>
    /// 豆包 Pro 模型
    /// 特点：中文理解优秀、对话能力强、本土化优势
    /// </summary>
    Doubao
}

/// <summary>
/// TTS 引擎类型扩展方法
/// </summary>
public static class TtsEngineTypeExtensions
{
    /// <summary>
    /// 获取 TTS 引擎的显示名称
    /// </summary>
    /// <param name="engineType">引擎类型</param>
    /// <returns>显示名称</returns>
    public static string GetDisplayName(this TtsEngineType engineType)
    {
        return engineType switch
        {
            TtsEngineType.Edge => "Microsoft Edge TTS",
            TtsEngineType.OpenAi => "OpenAI TTS",
            TtsEngineType.Doubao => "豆包 TTS",
            TtsEngineType.Kokoro => "Kokoro TTS",
            _ => engineType.ToString()
        };
    }

    /// <summary>
    /// 获取 TTS 引擎的描述信息
    /// </summary>
    /// <param name="engineType">引擎类型</param>
    /// <returns>描述信息</returns>
    public static string GetDescription(this TtsEngineType engineType)
    {
        return engineType switch
        {
            TtsEngineType.Edge => "微软提供的免费TTS服务，支持多种语言，音质良好",
            TtsEngineType.OpenAi => "OpenAI提供的AI驱动TTS服务，语音自然，支持多种风格",
            TtsEngineType.Doubao => "字节跳动的TTS服务，中文优化，低延迟高音质",
            TtsEngineType.Kokoro => "专业的日文TTS服务，情感表达丰富",
            _ => "未知的TTS引擎"
        };
    }

    /// <summary>
    /// 检查引擎是否需要 API 密钥
    /// </summary>
    /// <param name="engineType">引擎类型</param>
    /// <returns>是否需要 API 密钥</returns>
    public static bool RequiresApiKey(this TtsEngineType engineType)
    {
        return engineType switch
        {
            TtsEngineType.Edge => false,
            TtsEngineType.OpenAi => true,
            TtsEngineType.Doubao => true,
            TtsEngineType.Kokoro => false,
            _ => false
        };
    }
}