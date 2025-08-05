using System.Text.Json.Serialization;

namespace EasyVoice.Core.Models;

/// <summary>
/// 音频配置
/// </summary>
public class AudioConfig
{
    /// <summary>
    /// 音频通道数
    /// </summary>
    [JsonPropertyName("channel")]
    public int Channel { get; set; } = 1;
    
    /// <summary>
    /// 音频格式
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "pcm";
    
    /// <summary>
    /// 采样率
    /// </summary>
    [JsonPropertyName("sample_rate")]
    public int SampleRate { get; set; } = 24000;
}

/// <summary>
/// TTS配置
/// </summary>
public class TtsConfig
{
    /// <summary>
    /// 音频配置
    /// </summary>
    [JsonPropertyName("audio_config")]
    public AudioConfig AudioConfig { get; set; } = new();
}

/// <summary>
/// 对话配置
/// </summary>
public class DialogConfig
{
    /// <summary>
    /// 对话ID
    /// </summary>
    [JsonPropertyName("dialog_id")]
    public string? DialogId { get; set; }
    
    /// <summary>
    /// 机器人名称
    /// </summary>
    [JsonPropertyName("bot_name")]
    public string BotName { get; set; } = "豆包";
    
    /// <summary>
    /// 系统角色设定
    /// </summary>
    [JsonPropertyName("system_role")]
    public string SystemRole { get; set; } = "你使用活泼灵动的女声，性格开朗，热爱生活。";
    
    /// <summary>
    /// 说话风格
    /// </summary>
    [JsonPropertyName("speaking_style")]
    public string SpeakingStyle { get; set; } = "你的说话风格简洁明了，语速适中，语调自然。";
    
    /// <summary>
    /// 额外配置
    /// </summary>
    [JsonPropertyName("extra")]
    public Dictionary<string, object> Extra { get; set; } = new()
    {
        ["strict_audit"] = false,
        ["audit_response"] = "抱歉这个问题我无法回答，你可以换个其他话题，我会尽力为你提供帮助。"
    };
}

/// <summary>
/// 开始会话请求载荷
/// </summary>
public class StartSessionPayload
{
    /// <summary>
    /// TTS配置
    /// </summary>
    [JsonPropertyName("tts")]
    public TtsConfig Tts { get; set; } = new();
    
    /// <summary>
    /// 对话配置
    /// </summary>
    [JsonPropertyName("dialog")]
    public DialogConfig Dialog { get; set; } = new();
}

/// <summary>
/// 问候语请求载荷
/// </summary>
public class SayHelloPayload
{
    /// <summary>
    /// 问候内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 聊天TTS文本请求载荷
/// </summary>
public class ChatTtsTextPayload
{
    /// <summary>
    /// 是否开始
    /// </summary>
    [JsonPropertyName("start")]
    public bool Start { get; set; }
    
    /// <summary>
    /// 是否结束
    /// </summary>
    [JsonPropertyName("end")]
    public bool End { get; set; }
    
    /// <summary>
    /// 文本内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 实时对话连接配置
/// </summary>
public class RealTimeConnectionConfig
{
    /// <summary>
    /// WebSocket URL
    /// </summary>
    public string WebSocketUrl { get; set; } = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue";
    
    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;
    
    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// 连接超时时间（毫秒）
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// 心跳间隔（毫秒）
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 30000;
    
    /// <summary>
    /// 音频缓冲区大小（秒）
    /// </summary>
    public int AudioBufferSeconds { get; set; } = 100;
    
    /// <summary>
    /// 音频帧大小
    /// </summary>
    public int AudioFrameSize { get; set; } = 160;
    
    /// <summary>
    /// 输入音频采样率
    /// </summary>
    public int InputSampleRate { get; set; } = 16000;
    
    /// <summary>
    /// 输出音频采样率
    /// </summary>
    public int OutputSampleRate { get; set; } = 24000;
}

/// <summary>
/// 实时对话状态
/// </summary>
public enum RealTimeDialogState
{
    /// <summary>
    /// 未连接
    /// </summary>
    Disconnected,
    
    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,
    
    /// <summary>
    /// 已连接
    /// </summary>
    Connected,
    
    /// <summary>
    /// 会话中
    /// </summary>
    InSession,
    
    /// <summary>
    /// 错误状态
    /// </summary>
    Error,
    
    /// <summary>
    /// 正在断开连接
    /// </summary>
    Disconnecting
}

/// <summary>
/// 音频数据事件参数
/// </summary>
public class AudioDataEventArgs : EventArgs
{
    /// <summary>
    /// 音频数据
    /// </summary>
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// 音频格式
    /// </summary>
    public string Format { get; set; } = "pcm";
    
    /// <summary>
    /// 采样率
    /// </summary>
    public int SampleRate { get; set; }
    
    /// <summary>
    /// 通道数
    /// </summary>
    public int Channels { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 对话事件参数
/// </summary>
public class DialogEventArgs : EventArgs
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public RealTimeEventType EventType { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// 事件数据
    /// </summary>
    public object? Data { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 错误事件参数
/// </summary>
public class ErrorEventArgs : EventArgs
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public uint ErrorCode { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 连接状态变化事件参数
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 旧状态
    /// </summary>
    public RealTimeDialogState OldState { get; set; }
    
    /// <summary>
    /// 新状态
    /// </summary>
    public RealTimeDialogState NewState { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 会话信息
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 连接ID
    /// </summary>
    public string? ConnectId { get; set; }
    
    /// <summary>
    /// 对话ID
    /// </summary>
    public string? DialogId { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 会话状态
    /// </summary>
    public RealTimeDialogState State { get; set; } = RealTimeDialogState.Disconnected;
}