using System.Text.Json.Serialization;
using EasyVoice.RealtimeDialog.Models.Audio;

namespace EasyVoice.RealtimeDialog.Models.Session;

/// <summary>
/// 对话会话
/// </summary>
public class DialogSession
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// 会话状态
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Created;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndedAt { get; set; }
    
    /// <summary>
    /// 会话配置
    /// </summary>
    public SessionConfig Config { get; set; } = new();
    
    /// <summary>
    /// 会话持续时间（秒）
    /// </summary>
    public int DurationSeconds => EndedAt.HasValue && StartedAt.HasValue 
        ? (int)(EndedAt.Value - StartedAt.Value).TotalSeconds 
        : 0;
    
    /// <summary>
    /// 消息总数
    /// </summary>
    public int TotalMessages { get; set; }
    
    /// <summary>
    /// 音频消息数
    /// </summary>
    public int AudioMessages { get; set; }
    
    /// <summary>
    /// 文本消息数
    /// </summary>
    public int TextMessages { get; set; }
    
    /// <summary>
    /// 错误次数
    /// </summary>
    public int ErrorCount { get; set; }
    
    /// <summary>
    /// 最后一次活动时间
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 会话元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// 会话消息列表
    /// </summary>
    public List<SessionMessage> Messages { get; set; } = new();
    
    /// <summary>
    /// 开始会话
    /// </summary>
    public void Start()
    {
        Status = SessionStatus.Active;
        StartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        LastActivityAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="reason">结束原因</param>
    public void End(string? reason = null)
    {
        Status = SessionStatus.Finished;
        EndedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(reason))
        {
            Metadata["end_reason"] = reason;
        }
    }
    
    /// <summary>
    /// 标记会话出错
    /// </summary>
    /// <param name="error">错误信息</param>
    public void MarkError(string error)
    {
        Status = SessionStatus.Error;
        ErrorCount++;
        UpdatedAt = DateTime.UtcNow;
        Metadata["last_error"] = error;
        Metadata["error_time"] = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 更新活动时间
    /// </summary>
    public void UpdateActivity()
    {
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 检查会话是否超时
    /// </summary>
    /// <param name="timeoutMinutes">超时时间（分钟）</param>
    /// <returns>是否超时</returns>
    public bool IsTimeout(int timeoutMinutes = 30)
    {
        return DateTime.UtcNow - LastActivityAt > TimeSpan.FromMinutes(timeoutMinutes);
    }
}

/// <summary>
/// 会话状态
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// 已创建
    /// </summary>
    Created,
    
    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,
    
    /// <summary>
    /// 活跃中
    /// </summary>
    Active,
    
    /// <summary>
    /// 暂停
    /// </summary>
    Paused,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Finished,
    
    /// <summary>
    /// 错误状态
    /// </summary>
    Error,
    
    /// <summary>
    /// 超时
    /// </summary>
    Timeout
}

/// <summary>
/// 会话配置
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// 机器人名称
    /// </summary>
    [JsonPropertyName("bot_name")]
    public string BotName { get; set; } = "豆包";
    
    /// <summary>
    /// 系统角色设定
    /// </summary>
    [JsonPropertyName("system_role")]
    public string SystemRole { get; set; } = "你是一个友善的AI助手，使用活泼灵动的女声，性格开朗，热爱生活。";
    
    /// <summary>
    /// 说话风格
    /// </summary>
    [JsonPropertyName("speaking_style")]
    public string SpeakingStyle { get; set; } = "你的说话风格简洁明了，语速适中，语调自然。";
    
    /// <summary>
    /// 音频配置
    /// </summary>
    [JsonPropertyName("audio_config")]
    public AudioConfig AudioConfig { get; set; } = new();
    
    /// <summary>
    /// 语音配置
    /// </summary>
    [JsonPropertyName("voice_config")]
    public VoiceConfig VoiceConfig { get; set; } = new();
    
    /// <summary>
    /// 用户配置
    /// </summary>
    [JsonPropertyName("user_config")]
    public UserConfig UserConfig { get; set; } = new();
    
    /// <summary>
    /// 会话超时时间（分钟）
    /// </summary>
    [JsonPropertyName("timeout_minutes")]
    public int TimeoutMinutes { get; set; } = 30;
    
    /// <summary>
    /// 最大消息数限制
    /// </summary>
    [JsonPropertyName("max_messages")]
    public int MaxMessages { get; set; } = 1000;
    
    /// <summary>
    /// 是否启用自动TTS
    /// </summary>
    [JsonPropertyName("auto_tts_enabled")]
    public bool AutoTtsEnabled { get; set; } = true;
    
    /// <summary>
    /// 是否启用情感识别
    /// </summary>
    [JsonPropertyName("emotion_recognition_enabled")]
    public bool EmotionRecognitionEnabled { get; set; } = true;
    
    /// <summary>
    /// 扩展配置
    /// </summary>
    [JsonPropertyName("extensions")]
    public Dictionary<string, object> Extensions { get; set; } = new();
}

/// <summary>
/// 语音配置
/// </summary>
public class VoiceConfig
{
    /// <summary>
    /// 语音ID
    /// </summary>
    [JsonPropertyName("voice_id")]
    public string VoiceId { get; set; } = "zh_female_qingxin";
    
    /// <summary>
    /// 语速（0.5-2.0）
    /// </summary>
    [JsonPropertyName("speed")]
    public float Speed { get; set; } = 1.0f;
    
    /// <summary>
    /// 音调（0.5-2.0）
    /// </summary>
    [JsonPropertyName("pitch")]
    public float Pitch { get; set; } = 1.0f;
    
    /// <summary>
    /// 音量（0.0-1.0）
    /// </summary>
    [JsonPropertyName("volume")]
    public float Volume { get; set; } = 1.0f;
    
    /// <summary>
    /// 情感强度（0.0-1.0）
    /// </summary>
    [JsonPropertyName("emotion_intensity")]
    public float EmotionIntensity { get; set; } = 0.5f;
    
    /// <summary>
    /// 语言代码
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "zh-CN";
    
    /// <summary>
    /// 音频格式
    /// </summary>
    [JsonPropertyName("audio_format")]
    public string AudioFormat { get; set; } = "pcm";
}

/// <summary>
/// 用户配置
/// </summary>
public class UserConfig
{
    /// <summary>
    /// 用户偏好语言
    /// </summary>
    [JsonPropertyName("preferred_language")]
    public string PreferredLanguage { get; set; } = "zh-CN";
    
    /// <summary>
    /// 用户名称
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }
    
    /// <summary>
    /// 用户年龄段
    /// </summary>
    [JsonPropertyName("age_group")]
    public string? AgeGroup { get; set; }
    
    /// <summary>
    /// 用户兴趣标签
    /// </summary>
    [JsonPropertyName("interests")]
    public string[] Interests { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// 对话历史上下文长度
    /// </summary>
    [JsonPropertyName("context_length")]
    public int ContextLength { get; set; } = 10;
    
    /// <summary>
    /// 是否启用个性化
    /// </summary>
    [JsonPropertyName("personalization_enabled")]
    public bool PersonalizationEnabled { get; set; } = true;
    
    /// <summary>
    /// 用户自定义设置
    /// </summary>
    [JsonPropertyName("custom_settings")]
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// 会话消息
/// </summary>
public class SessionMessage
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageType MessageType { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 是否为用户消息
    /// </summary>
    public bool IsUserMessage { get; set; }
    
    /// <summary>
    /// 序列号
    /// </summary>
    public uint SequenceNumber { get; set; }
    
    /// <summary>
    /// 文本内容
    /// </summary>
    public string? TextContent { get; set; }
    
    /// <summary>
    /// 音频数据
    /// </summary>
    public byte[]? AudioData { get; set; }
    
    /// <summary>
    /// 音频格式
    /// </summary>
    public Protocol.AudioFormat? AudioFormat { get; set; }
    
    /// <summary>
    /// 消息元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// 处理状态
    /// </summary>
    public MessageProcessingStatus ProcessingStatus { get; set; } = MessageProcessingStatus.Pending;
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 消息类型
/// </summary>
public enum MessageType
{
    /// <summary>
    /// 文本消息
    /// </summary>
    Text,
    
    /// <summary>
    /// 音频消息
    /// </summary>
    Audio,
    
    /// <summary>
    /// 混合消息（文本+音频）
    /// </summary>
    Mixed,
    
    /// <summary>
    /// 控制消息
    /// </summary>
    Control,
    
    /// <summary>
    /// 系统消息
    /// </summary>
    System
}

/// <summary>
/// 消息处理状态
/// </summary>
public enum MessageProcessingStatus
{
    /// <summary>
    /// 待处理
    /// </summary>
    Pending,
    
    /// <summary>
    /// 处理中
    /// </summary>
    Processing,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 失败
    /// </summary>
    Failed,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}