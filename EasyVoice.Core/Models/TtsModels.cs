using EasyVoice.Core.Constants;

namespace EasyVoice.Core.Models;

/// <summary>
/// TTS 请求模型
/// 统一的文本转语音请求参数
/// </summary>
public class TtsRequest
{
    /// <summary>
    /// 要转换的文本内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 语音名称
    /// 使用 VoiceConstants 中定义的常量
    /// </summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>
    /// 音频输出格式
    /// </summary>
    public AudioFormat ResponseFormat { get; set; } = AudioFormat.Mp3;

    /// <summary>
    /// 输出文件路径（可选）
    /// 如果不指定，将返回音频流
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// 语音速度
    /// 范围：0.25 - 4.0，默认 1.0
    /// </summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>
    /// 音调调节（Edge TTS 专用）
    /// 格式："+0%", "-10%", "+20%" 等
    /// </summary>
    public string? Pitch { get; set; }

    /// <summary>
    /// 音量调节（Edge TTS 专用）
    /// 格式："0%", "10%", "20%" 等
    /// </summary>
    public string? Volume { get; set; }

    /// <summary>
    /// 请求标识符（用于跟踪和缓存）
    /// </summary>
    public string? RequestId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 额外的引擎特定参数
    /// </summary>
    public Dictionary<string, object>? ExtraParameters { get; set; }
}

/// <summary>
/// TTS 响应模型
/// 统一的文本转语音响应结果
/// </summary>
public class TtsResponse
{
    /// <summary>
    /// 请求是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 音频数据流
    /// </summary>
    public Stream? AudioStream { get; set; }

    /// <summary>
    /// 音频数据字节数组
    /// </summary>
    public byte[]? AudioData { get; set; }

    /// <summary>
    /// 输出文件路径（如果保存到文件）
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 音频格式
    /// </summary>
    public AudioFormat Format { get; set; }

    /// <summary>
    /// 音频时长（秒）
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// 音频文件大小（字节）
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// 使用的 TTS 引擎
    /// </summary>
    public TtsEngineType Engine { get; set; }

    /// <summary>
    /// 使用的语音名称
    /// </summary>
    public string? Voice { get; set; }

    /// <summary>
    /// 处理开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 处理结束时间
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// 处理耗时（毫秒）
    /// </summary>
    public long ProcessingTimeMs => (long)(EndTime - StartTime).TotalMilliseconds;

    /// <summary>
    /// 请求标识符
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 是否使用了缓存
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// 缓存键（如果使用了缓存）
    /// </summary>
    public string? CacheKey { get; set; }

    /// <summary>
    /// 额外的响应数据
    /// </summary>
    public Dictionary<string, object>? ExtraData { get; set; }
}

/// <summary>
/// LLM 配置模型
/// </summary>
public class LlmConfiguration
{
    /// <summary>
    /// LLM 模型类型
    /// </summary>
    public LlmModelType ModelType { get; set; }

    /// <summary>
    /// 模型名称（用于 API 调用）
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// API 端点地址
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API 密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 最大令牌数
    /// </summary>
    public int MaxTokens { get; set; } = 3000;

    /// <summary>
    /// 额外的配置参数
    /// </summary>
    public Dictionary<string, object>? ExtraSettings { get; set; }
}

/// <summary>
/// TTS 请求验证扩展方法
/// </summary>
public static class TtsRequestExtensions
{
    /// <summary>
    /// 验证 TTS 请求参数的有效性
    /// </summary>
    /// <param name="request">TTS 请求</param>
    /// <returns>验证结果</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(this TtsRequest request)
    {
        if (request == null)
            return (false, "请求对象不能为空");

        if (string.IsNullOrWhiteSpace(request.Text))
            return (false, "文本内容不能为空");

        if (request.Text.Length > 5000)
            return (false, "文本长度不能超过 5000 个字符");

        if (string.IsNullOrWhiteSpace(request.Voice))
            return (false, "语音名称不能为空");
        
        return (true, null);
    }
}

/// <summary>
/// TTS 响应扩展方法
/// </summary>
public static class TtsResponseExtensions
{
    /// <summary>
    /// 创建成功响应
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <param name="format">音频格式</param>
    /// <param name="engine">使用的引擎</param>
    /// <param name="voice">使用的语音</param>
    /// <param name="requestId">请求 ID</param>
    /// <returns>成功响应</returns>
    public static TtsResponse CreateSuccess(
        byte[] audioData,
        AudioFormat format,
        TtsEngineType engine,
        string voice,
        string? requestId = null)
    {
        return new TtsResponse
        {
            Success = true,
            AudioData = audioData,
            AudioStream = new MemoryStream(audioData),
            Format = format,
            Engine = engine,
            Voice = voice,
            RequestId = requestId,
            FileSize = audioData.Length,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="engine">使用的引擎</param>
    /// <param name="requestId">请求 ID</param>
    /// <returns>失败响应</returns>
    public static TtsResponse CreateError(
        string errorMessage,
        TtsEngineType engine,
        string? requestId = null)
    {
        return new TtsResponse
        {
            Success = false,
            ErrorMessage = errorMessage,
            Engine = engine,
            RequestId = requestId,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };
    }
}