using EasyVoice.RealtimeDialog.Models.Audio;

namespace EasyVoice.RealtimeDialog.Models;

/// <summary>
/// 会话配置模型
/// </summary>
public class SessionConfig
{
    public string AppId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public AudioConfig AudioConfig { get; set; } = new();
    public string? BotName { get; set; }
    public string? SystemRole { get; set; }
    public string? SpeakingStyle { get; set; }
    public string? Cluster { get; set; }
    public string? VoiceType { get; set; }
    public string? AudioEncoding { get; set; }
    public int? SampleRate { get; set; }
    public bool? EnableServerVad { get; set; }
}

/// <summary>
/// 会话信息模型
/// </summary>
public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public SessionConfig Config { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Created";
    public string ConnectionId { get; set; } = string.Empty;
    public DoubaoAudioManager? AudioManager { get; set; }
}

/// <summary>
/// 对话事件模型
/// </summary>
public class DialogEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public object? Data { get; set; }
}

/// <summary>
/// 音频数据模型
/// </summary>
public class AudioData
{
    public string SessionId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public string Direction { get; set; } = string.Empty; // "Input" or "Output"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}