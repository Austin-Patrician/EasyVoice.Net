namespace EasyVoice.RealtimeDialog.Models.Protocol;

/// <summary>
/// 豆包实时语音协议消息类型
/// </summary>
public enum MessageType : byte
{
    /// <summary>
    /// 客户端完整请求（包含音频和文本）
    /// </summary>
    ClientFullRequest = 0x01,
    
    /// <summary>
    /// 客户端仅音频请求
    /// </summary>
    ClientAudioOnlyRequest = 0x02,
    
    /// <summary>
    /// 服务器完整响应
    /// </summary>
    ServerFullResponse = 0x11,
    
    /// <summary>
    /// 服务器确认响应
    /// </summary>
    ServerAck = 0x12,
    
    /// <summary>
    /// 服务器错误响应
    /// </summary>
    ServerErrorResponse = 0x13,
    
    /// <summary>
    /// 心跳消息
    /// </summary>
    Heartbeat = 0x20,
    
    /// <summary>
    /// 会话开始标记
    /// </summary>
    SessionStart = 0x30,
    
    /// <summary>
    /// 会话结束标记
    /// </summary>
    SessionEnd = 0x31,
    
    /// <summary>
    /// TTS触发事件
    /// </summary>
    TtsTrigger = 0x40
}

/// <summary>
/// 序列化方法
/// </summary>
public enum SerializationMethod : byte
{
    /// <summary>
    /// 无序列化（原始数据）
    /// </summary>
    NoSerialization = 0x00,
    
    /// <summary>
    /// JSON序列化
    /// </summary>
    Json = 0x01,
    
    /// <summary>
    /// Protocol Buffers序列化
    /// </summary>
    ProtocolBuffers = 0x02
}

/// <summary>
/// 压缩方式
/// </summary>
public enum CompressionMethod : byte
{
    /// <summary>
    /// 无压缩
    /// </summary>
    NoCompression = 0x00,
    
    /// <summary>
    /// GZIP压缩
    /// </summary>
    Gzip = 0x01,
    
    /// <summary>
    /// LZ4压缩
    /// </summary>
    Lz4 = 0x02
}

/// <summary>
/// 消息标志
/// </summary>
[Flags]
public enum MessageFlags : byte
{
    /// <summary>
    /// 无标志
    /// </summary>
    None = 0x00,
    
    /// <summary>
    /// 包含事件数据
    /// </summary>
    WithEvent = 0x01,
    
    /// <summary>
    /// 正序列号
    /// </summary>
    PositiveSequence = 0x02,
    
    /// <summary>
    /// 负序列号
    /// </summary>
    NegativeSequence = 0x04,
    
    /// <summary>
    /// 需要确认
    /// </summary>
    RequireAck = 0x08,
    
    /// <summary>
    /// 最后一个分片
    /// </summary>
    LastFragment = 0x10
}