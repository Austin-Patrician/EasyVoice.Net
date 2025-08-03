using EasyVoice.Core.Constants;
using EasyVoice.Core.Models;

namespace EasyVoice.Core.Interfaces;

/// <summary>
/// LLM 增强的 TTS 服务接口
/// 提供集成大语言模型的智能文本转语音功能
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// 使用 OpenAI TTS 生成语音
    /// 支持多种语音风格和高质量的英文语音合成
    /// </summary>
    /// <param name="request">TTS 请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>TTS 响应结果</returns>
    /// <example>
    /// <code>
    /// var request = new TtsRequest
    /// {
    ///     Text = "Hello, this is OpenAI TTS test.",
    ///     Voice = VoiceConstants.OpenAI.ALLOY,
    ///     ResponseFormat = AudioFormat.Mp3,
    ///     Speed = 1.0f
    /// };
    /// var response = await llmService.GenerateWithOpenAIAsync(request);
    /// </code>
    /// </example>
    Task<TtsResponse> GenerateWithOpenAiAsync(TtsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用豆包 TTS 生成语音
    /// 专为中文优化，支持低延迟和高音质的中文语音合成
    /// </summary>
    /// <param name="request">TTS 请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>TTS 响应结果</returns>
    /// <example>
    /// <code>
    /// var request = new TtsRequest
    /// {
    ///     Text = "你好，这是豆包 TTS 测试。",
    ///     Voice = VoiceConstants.Doubao.ZH_FEMALE_1,
    ///     ResponseFormat = AudioFormat.Mp3,
    ///     Speed = 1.0f
    /// };
    /// var response = await llmService.GenerateWithDoubaoAsync(request);
    /// </code>
    /// </example>
    Task<TtsResponse> GenerateWithDoubaoAsync(TtsRequest request, CancellationToken cancellationToken = default);


    /// <summary>
    /// 使用豆包 TTS 生成语音 返回stream
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Stream> GenerateWithDoubaoStreamAsync(TtsRequest request,
        CancellationToken cancellationToken = default);

    
    /// <summary>
    /// 使用 OpenAI TTS 生成语音 返回stream
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Stream> GenerateWithOpenAiStreamAsync(TtsRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 使用 Kokoro TTS 生成语音
    /// 专为日文优化，支持丰富的情感表达
    /// </summary>
    /// <param name="request">TTS 请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>TTS 响应结果</returns>
    /// <example>
    /// <code>
    /// var request = new TtsRequest
    /// {
    ///     Text = "こんにちは、これは Kokoro TTS のテストです。",
    ///     Voice = VoiceConstants.Kokoro.KOKORO,
    ///     ResponseFormat = AudioFormat.Mp3,
    ///     Speed = 1.0f
    /// };
    /// var response = await llmService.GenerateWithKokoroAsync(request);
    /// </code>
    /// </example>
    Task<TtsResponse> GenerateWithKokoroAsync(TtsRequest request, CancellationToken cancellationToken = default);
   
    /// <summary>
    /// 获取支持的 TTS 引擎列表
    /// </summary>
    /// <returns>支持的 TTS 引擎类型</returns>
    IEnumerable<TtsEngineType> GetSupportedEngines();
}


/// <summary>
/// LLM 分析结果
/// </summary>
public class LlmAnalysisResult
{
    /// <summary>
    /// 检测到的主要语言
    /// </summary>
    public string DetectedLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 语言置信度（0.0 - 1.0）
    /// </summary>
    public float LanguageConfidence { get; set; }

    /// <summary>
    /// 情感色调
    /// </summary>
    public EmotionTone EmotionTone { get; set; }

    /// <summary>
    /// 情感强度（0.0 - 1.0）
    /// </summary>
    public float EmotionIntensity { get; set; }

    /// <summary>
    /// 文本分段结果
    /// </summary>
    public List<TextSegment> Segments { get; set; } = new();

    /// <summary>
    /// 推荐的 TTS 引擎
    /// </summary>
    public TtsEngineType RecommendedEngine { get; set; }

    /// <summary>
    /// 推荐的语音
    /// </summary>
    public string RecommendedVoice { get; set; } = string.Empty;

    /// <summary>
    /// 推荐理由
    /// </summary>
    public string RecommendationReason { get; set; } = string.Empty;

    /// <summary>
    /// 推荐置信度（0.0 - 1.0）
    /// </summary>
    public float RecommendationConfidence { get; set; }
    
    /// <summary>
    /// 使用的 LLM 模型
    /// </summary>
    public LlmModelType UsedModel { get; set; }
}

/// <summary>
/// 语音推荐结果
/// </summary>
public class VoiceRecommendation
{
    /// <summary>
    /// 推荐的 TTS 引擎
    /// </summary>
    public TtsEngineType RecommendedEngine { get; set; }

    /// <summary>
    /// 推荐的语音
    /// </summary>
    public string RecommendedVoice { get; set; } = string.Empty;

    /// <summary>
    /// 推荐的语音参数
    /// </summary>
    public TtsRequest RecommendedParameters { get; set; } = new();

    /// <summary>
    /// 推荐理由
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 置信度（0.0 - 1.0）
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// 备选推荐
    /// </summary>
    public List<VoiceRecommendation> Alternatives { get; set; } = new();
}

/// <summary>
/// 智能 TTS 生成选项
/// </summary>
public class IntelligentTtsOptions
{
    /// <summary>
    /// 启用语言检测
    /// </summary>
    public bool EnableLanguageDetection { get; set; } = true;

    /// <summary>
    /// 启用文本分段
    /// </summary>
    public bool EnableTextSegmentation { get; set; } = false;

    /// <summary>
    /// 启用情感调节
    /// </summary>
    public bool EnableEmotionAdjustment { get; set; } = true;

    /// <summary>
    /// 输出格式偏好
    /// </summary>
    public AudioFormat PreferredFormat { get; set; } = AudioFormat.Mp3;

    /// <summary>
    /// 质量偏好
    /// </summary>
    public QualityPreference QualityPreference { get; set; } = QualityPreference.Balanced;

    /// <summary>
    /// 输出目录
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// 是否合并多段音频
    /// </summary>
    public bool MergeSegments { get; set; } = true;
}

/// <summary>
/// 引擎使用统计
/// </summary>
public class EngineUsageStats
{
    /// <summary>
    /// 引擎类型
    /// </summary>
    public TtsEngineType EngineType { get; set; }

    /// <summary>
    /// 总请求次数
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// 成功请求次数
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// 失败请求次数
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// 平均响应时间（毫秒）
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// 总处理的文本字符数
    /// </summary>
    public long TotalCharactersProcessed { get; set; }

    /// <summary>
    /// 总生成的音频时长（秒）
    /// </summary>
    public double TotalAudioDurationSeconds { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime LastUsedAt { get; set; }
}

/// <summary>
/// 文本段落
/// </summary>
public class TextSegment
{
    /// <summary>
    /// 段落文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 开始位置
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// 结束位置
    /// </summary>
    public int EndIndex { get; set; }

    /// <summary>
    /// 段落类型
    /// </summary>
    public SegmentType Type { get; set; }

    /// <summary>
    /// 建议的语音参数
    /// </summary>
    public TtsRequest? SuggestedParameters { get; set; }
}

/// <summary>
/// 情感色调枚举
/// </summary>
public enum EmotionTone
{
    /// <summary>
    /// 中性
    /// </summary>
    Neutral,

    /// <summary>
    /// 快乐
    /// </summary>
    Happy,

    /// <summary>
    /// 悲伤
    /// </summary>
    Sad,

    /// <summary>
    /// 兴奋
    /// </summary>
    Excited,

    /// <summary>
    /// 平静
    /// </summary>
    Calm,

    /// <summary>
    /// 愤怒
    /// </summary>
    Angry,

    /// <summary>
    /// 温柔
    /// </summary>
    Gentle,

    /// <summary>
    /// 严肃
    /// </summary>
    Serious
}

/// <summary>
/// 分析深度级别
/// </summary>
public enum AnalysisDepth
{
    /// <summary>
    /// 基础分析
    /// </summary>
    Basic,

    /// <summary>
    /// 标准分析
    /// </summary>
    Standard,

    /// <summary>
    /// 深度分析
    /// </summary>
    Deep,

    /// <summary>
    /// 详细分析
    /// </summary>
    Detailed
}

/// <summary>
/// 质量偏好
/// </summary>
public enum QualityPreference
{
    /// <summary>
    /// 速度优先
    /// </summary>
    Speed,

    /// <summary>
    /// 平衡
    /// </summary>
    Balanced,

    /// <summary>
    /// 质量优先
    /// </summary>
    Quality
}

/// <summary>
/// 段落类型
/// </summary>
public enum SegmentType
{
    /// <summary>
    /// 普通段落
    /// </summary>
    Normal,

    /// <summary>
    /// 标题
    /// </summary>
    Title,

    /// <summary>
    /// 引用
    /// </summary>
    Quote,

    /// <summary>
    /// 对话
    /// </summary>
    Dialogue,

    /// <summary>
    /// 叙述
    /// </summary>
    Narrative
}
