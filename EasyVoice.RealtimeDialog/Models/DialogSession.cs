using EasyVoice.RealtimeDialog.Models.Audio;

namespace EasyVoice.RealtimeDialog.Models;

/// <summary>
/// 会话状态枚举
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// 初始化中
    /// </summary>
    Starting,
    
    /// <summary>
    /// 活跃状态
    /// </summary>
    Active,
    
    /// <summary>
    /// 结束中
    /// </summary>
    Ending,
    
    /// <summary>
    /// 已结束
    /// </summary>
    Ended,
    
    /// <summary>
    /// 失败
    /// </summary>
    Failed,
    
    /// <summary>
    /// 断开连接
    /// </summary>
    Disconnected
}

/// <summary>
/// 对话会话模型
/// </summary>
public class DialogSession
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 会话配置
    /// </summary>
    public SessionConfig Config { get; set; } = new();
    
    /// <summary>
    /// 会话状态
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Starting;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTimeOffset LastActivityTime { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// 是否正在用户查询
    /// </summary>
    public bool IsUserQuerying { get; set; }
    
    /// <summary>
    /// 是否正在发送ChatTTSText
    /// </summary>
    public bool IsSendingChatTtsText { get; set; }
    
    /// <summary>
    /// 音频缓存
    /// </summary>
    public List<byte> AudioBuffer { get; set; } = new();
    
    /// <summary>
    /// 连接ID（可选）
    /// </summary>
    public string? ConnectId { get; set; }
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 会话配置模型
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;
    
    /// <summary>
    /// 访问密钥
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频配置
    /// </summary>
    public AudioConfig AudioConfig { get; set; } = new();
    
    /// <summary>
    /// 说话人
    /// </summary>
    public string? Speaker { get; set; }
    
    /// <summary>
    /// 机器人名称
    /// </summary>
    public string? BotName { get; set; }
    
    /// <summary>
    /// 系统角色
    /// </summary>
    public string? SystemRole { get; set; }
    
    /// <summary>
    /// 说话风格
    /// </summary>
    public string? SpeakingStyle { get; set; }
    
    /// <summary>
    /// 集群
    /// </summary>
    public string? Cluster { get; set; }
    
    /// <summary>
    /// 语音类型
    /// </summary>
    public string? VoiceType { get; set; }
    
    /// <summary>
    /// 音频编码
    /// </summary>
    public string? AudioEncoding { get; set; }
    
    /// <summary>
    /// 采样率
    /// </summary>
    public int? SampleRate { get; set; }
    
    /// <summary>
    /// 是否启用服务器VAD
    /// </summary>
    public bool EnableServerVad { get; set; } = true;
}