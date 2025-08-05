namespace EasyVoice.Core.Models;

/// <summary>
/// 语音类型信息
/// </summary>
public class VoiceTypeInfo
{
    /// <summary>
    /// 语音类型名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 语音类型描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 支持的语言代码
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 性别（male/female/neutral）
    /// </summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>
    /// 年龄范围（young/adult/elderly）
    /// </summary>
    public string AgeRange { get; set; } = string.Empty;

    /// <summary>
    /// 语音风格（formal/casual/emotional等）
    /// </summary>
    public string Style { get; set; } = string.Empty;
}

/// <summary>
/// 文本分析请求
/// </summary>
public class TextAnalysisRequest
{
    /// <summary>
    /// 要分析的文本内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 可用的语音类型列表
    /// </summary>
    public List<VoiceTypeInfo> AvailableVoiceTypes { get; set; } = new();
}

/// <summary>
/// 语音参数推荐结果
/// </summary>
public class VoiceRecommendation
{
    /// <summary>
    /// 推荐的语音类型
    /// </summary>
    public string RecommendedVoiceType { get; set; } = string.Empty;

    /// <summary>
    /// 推荐的语言
    /// </summary>
    public string RecommendedLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 推荐的语速（0.25 - 4.0）
    /// </summary>
    public float RecommendedSpeed { get; set; } = 1.0f;

    /// <summary>
    /// 推荐的音调（-50% 到 +200%）
    /// </summary>
    public string RecommendedPitch { get; set; } = "+0%";

    /// <summary>
    /// 推荐的音量（0% 到 100%）
    /// </summary>
    public string RecommendedVolume { get; set; } = "50%";

    /// <summary>
    /// 推荐理由
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// 置信度（0.0 - 1.0）
    /// </summary>
    public float Confidence { get; set; } = 0.0f;
}

/// <summary>
/// 文本分析结果
/// </summary>
public class TextAnalysisResult
{
    /// <summary>
    /// 分析是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 语音推荐结果
    /// </summary>
    public VoiceRecommendation? Recommendation { get; set; }

    /// <summary>
    /// 文本分析的详细信息
    /// </summary>
    public TextAnalysisDetails? AnalysisDetails { get; set; }
}

/// <summary>
/// 文本分析详细信息
/// </summary>
public class TextAnalysisDetails
{
    /// <summary>
    /// 检测到的主要语言
    /// </summary>
    public string DetectedLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 文本情感（positive/negative/neutral）
    /// </summary>
    public string Sentiment { get; set; } = string.Empty;

    /// <summary>
    /// 文本类型（narrative/dialogue/formal/casual等）
    /// </summary>
    public string TextType { get; set; } = string.Empty;

    /// <summary>
    /// 文本长度
    /// </summary>
    public int TextLength { get; set; }

    /// <summary>
    /// 预估阅读时间（秒）
    /// </summary>
    public double EstimatedReadingTime { get; set; }

    /// <summary>
    /// 关键词列表
    /// </summary>
    public List<string> Keywords { get; set; } = new();
}