using System.Text.Json.Serialization;
using EasyVoice.RealtimeDialog.Models.Session;

namespace EasyVoice.RealtimeDialog.Models.Protocol;

/// <summary>
/// 协议消息基类
/// </summary>
public abstract class ProtocolMessage
{
    /// <summary>
    /// 消息头部
    /// </summary>
    [JsonIgnore]
    public MessageHeader Header { get; set; }
    
    /// <summary>
    /// 消息ID
    /// </summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 客户端完整请求消息
/// </summary>
public class ClientFullRequestMessage : ProtocolMessage
{
    /// <summary>
    /// 音频数据（Base64编码）
    /// </summary>
    [JsonPropertyName("audio_data")]
    public string AudioData { get; set; } = string.Empty;
    
    /// <summary>
    /// 文本内容
    /// </summary>
    [JsonPropertyName("text_content")]
    public string TextContent { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频格式
    /// </summary>
    [JsonPropertyName("audio_format")]
    public AudioFormat AudioFormat { get; set; } = new();
    
    /// <summary>
    /// 是否为最后一个音频片段
    /// </summary>
    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// 用户配置
    /// </summary>
    [JsonPropertyName("user_config")]
    public UserConfig? UserConfig { get; set; }
}

/// <summary>
/// 客户端仅音频请求消息
/// </summary>
public class ClientAudioOnlyRequestMessage : ProtocolMessage
{
    /// <summary>
    /// 音频数据（Base64编码）
    /// </summary>
    [JsonPropertyName("audio_data")]
    public string AudioData { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频格式
    /// </summary>
    [JsonPropertyName("audio_format")]
    public AudioFormat AudioFormat { get; set; } = new();
    
    /// <summary>
    /// 是否为最后一个音频片段
    /// </summary>
    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// 音频序列号
    /// </summary>
    [JsonPropertyName("audio_sequence")]
    public uint AudioSequence { get; set; }
}

/// <summary>
/// 服务器完整响应消息
/// </summary>
public class ServerFullResponseMessage : ProtocolMessage
{
    /// <summary>
    /// 响应文本
    /// </summary>
    [JsonPropertyName("response_text")]
    public string ResponseText { get; set; } = string.Empty;
    
    /// <summary>
    /// TTS音频数据（Base64编码）
    /// </summary>
    [JsonPropertyName("tts_audio")]
    public string TtsAudio { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频格式
    /// </summary>
    [JsonPropertyName("audio_format")]
    public AudioFormat AudioFormat { get; set; } = new();
    
    /// <summary>
    /// 是否为最终响应
    /// </summary>
    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// 响应类型
    /// </summary>
    [JsonPropertyName("response_type")]
    public string ResponseType { get; set; } = "text_and_audio";
    
    /// <summary>
    /// 情感标签
    /// </summary>
    [JsonPropertyName("emotion")]
    public string? Emotion { get; set; }
}

/// <summary>
/// 服务器确认响应消息
/// </summary>
public class ServerAckMessage : ProtocolMessage
{
    /// <summary>
    /// 确认的消息ID
    /// </summary>
    [JsonPropertyName("ack_message_id")]
    public string AckMessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// 确认状态
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "received";
    
    /// <summary>
    /// 处理延迟（毫秒）
    /// </summary>
    [JsonPropertyName("processing_delay_ms")]
    public int ProcessingDelayMs { get; set; }
}

/// <summary>
/// 服务器错误响应消息
/// </summary>
public class ServerErrorResponseMessage : ProtocolMessage
{
    /// <summary>
    /// 错误代码
    /// </summary>
    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误详情
    /// </summary>
    [JsonPropertyName("error_details")]
    public Dictionary<string, object>? ErrorDetails { get; set; }
    
    /// <summary>
    /// 是否可重试
    /// </summary>
    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; }
}

/// <summary>
/// 心跳消息
/// </summary>
public class HeartbeatMessage : ProtocolMessage
{
    /// <summary>
    /// 心跳类型（ping/pong）
    /// </summary>
    [JsonPropertyName("heartbeat_type")]
    public string HeartbeatType { get; set; } = "ping";
    
    /// <summary>
    /// 客户端时间戳
    /// </summary>
    [JsonPropertyName("client_timestamp")]
    public long ClientTimestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 会话控制消息
/// </summary>
public class SessionControlMessage : ProtocolMessage
{
    /// <summary>
    /// 控制类型（start/end）
    /// </summary>
    [JsonPropertyName("control_type")]
    public string ControlType { get; set; } = string.Empty;
    
    /// <summary>
    /// 会话配置
    /// </summary>
    [JsonPropertyName("session_config")]
    public SessionConfig? SessionConfig { get; set; }
    
    /// <summary>
    /// 结束原因（仅在end时使用）
    /// </summary>
    [JsonPropertyName("end_reason")]
    public string? EndReason { get; set; }
}

/// <summary>
/// TTS触发事件消息
/// </summary>
public class TtsTriggerMessage : ProtocolMessage
{
    /// <summary>
    /// 触发文本
    /// </summary>
    [JsonPropertyName("trigger_text")]
    public string TriggerText { get; set; } = string.Empty;
    
    /// <summary>
    /// 语音配置
    /// </summary>
    [JsonPropertyName("voice_config")]
    public VoiceConfig? VoiceConfig { get; set; }
    
    /// <summary>
    /// 是否立即播放
    /// </summary>
    [JsonPropertyName("immediate_play")]
    public bool ImmediatePlay { get; set; } = true;
}