using System.Text.Json.Serialization;

namespace EasyVoice.Core.Models;

/// <summary>
/// 消息类型枚举
/// </summary>
public enum MessageType : int
{
    Invalid = 0,
    FullClient = 1,
    AudioOnlyClient = 2,
    FullServer = 9,
    AudioOnlyServer = 11,
    FrontEndResultServer = 12,
    Error = 15,
    ServerACK = AudioOnlyServer
}

/// <summary>
/// 消息类型标志位
/// </summary>
[Flags]
public enum MessageTypeFlags : byte
{
    NoSeq = 0,          // 无序列号的非终端包
    PositiveSeq = 1,    // 序列号 > 0 的非终端包
    LastNoSeq = 2,      // 无序列号的最后包
    NegativeSeq = 3,    // 序列号 < 0 的最后包
    WithEvent = 4       // 包含事件号的包
}

/// <summary>
/// 协议版本
/// </summary>
public enum ProtocolVersion : byte
{
    Version1 = 1,
    Version2 = 2,
    Version3 = 3,
    Version4 = 4
}

/// <summary>
/// 头部大小
/// </summary>
public enum HeaderSize : byte
{
    Size4 = 1,
    Size8 = 2,
    Size12 = 3,
    Size16 = 4
}

/// <summary>
/// 序列化方式
/// </summary>
public enum SerializationMethod : byte
{
    Raw = 0,
    JSON = 1,
    Thrift = 3,
    Custom = 15
}

/// <summary>
/// 压缩方式
/// </summary>
public enum CompressionMethod : byte
{
    None = 0,
    Gzip = 1,
    Custom = 15
}

/// <summary>
/// 实时对话事件类型
/// </summary>
public enum RealTimeEventType : int
{
    // 连接相关事件
    StartConnection = 1,
    FinishConnection = 2,
    ConnectionStarted = 50,
    ConnectionFailed = 51,
    ConnectionFinished = 52,
    
    // 会话相关事件
    StartSession = 100,
    FinishSession = 102,
    SessionStarted = 150,
    SessionFinished = 152,
    SessionFailed = 153,
    
    // 音频相关事件
    AudioData = 200,
    
    // 对话相关事件
    SayHello = 300,
    TtsResponse = 350,
    AsrInfo = 450,
    DialogFinished = 459,
    ChatTtsText = 500
}

/// <summary>
/// 二进制协议消息
/// </summary>
public class ProtocolMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageType Type { get; set; }
    
    /// <summary>
    /// 消息类型标志
    /// </summary>
    public MessageTypeFlags TypeFlags { get; set; }
    
    /// <summary>
    /// 事件类型
    /// </summary>
    public RealTimeEventType? Event { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// 连接ID
    /// </summary>
    public string? ConnectId { get; set; }
    
    /// <summary>
    /// 序列号
    /// </summary>
    public int? Sequence { get; set; }
    
    /// <summary>
    /// 错误代码
    /// </summary>
    public uint? ErrorCode { get; set; }
    
    /// <summary>
    /// 消息载荷
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// 请求ID
    /// </summary>
    public string? RequestId { get; set; }
}

/// <summary>
/// 二进制协议配置
/// </summary>
public class BinaryProtocolConfig
{
    /// <summary>
    /// 协议版本
    /// </summary>
    public ProtocolVersion Version { get; set; } = ProtocolVersion.Version1;
    
    /// <summary>
    /// 头部大小
    /// </summary>
    public HeaderSize HeaderSize { get; set; } = HeaderSize.Size4;
    
    /// <summary>
    /// 序列化方式
    /// </summary>
    public SerializationMethod Serialization { get; set; } = SerializationMethod.JSON;
    
    /// <summary>
    /// 压缩方式
    /// </summary>
    public CompressionMethod Compression { get; set; } = CompressionMethod.None;
    
    /// <summary>
    /// 是否包含序列号检查函数
    /// </summary>
    public Func<MessageTypeFlags, bool>? ContainsSequenceFunc { get; set; }
    
    /// <summary>
    /// 压缩函数
    /// </summary>
    public Func<byte[], byte[]>? CompressFunc { get; set; }
}

/// <summary>
/// 协议消息构建器
/// </summary>
public static class ProtocolMessageBuilder
{
    /// <summary>
    /// 创建新消息
    /// </summary>
    public static ProtocolMessage CreateMessage(MessageType messageType, MessageTypeFlags typeFlags)
    {
        return new ProtocolMessage
        {
            Type = messageType,
            TypeFlags = typeFlags,
            RequestId = Guid.NewGuid().ToString("N")
        };
    }
    
    /// <summary>
    /// 创建带事件的消息
    /// </summary>
    public static ProtocolMessage CreateEventMessage(MessageType messageType, RealTimeEventType eventType, string? sessionId = null)
    {
        return new ProtocolMessage
        {
            Type = messageType,
            TypeFlags = MessageTypeFlags.WithEvent,
            Event = eventType,
            SessionId = sessionId,
            RequestId = Guid.NewGuid().ToString("N")
        };
    }
    
    /// <summary>
    /// 创建音频消息
    /// </summary>
    public static ProtocolMessage CreateAudioMessage(byte[] audioData, string sessionId, int? sequence = null)
    {
        var message = new ProtocolMessage
        {
            Type = MessageType.AudioOnlyClient,
            TypeFlags = MessageTypeFlags.WithEvent,
            Event = RealTimeEventType.AudioData,
            SessionId = sessionId,
            Payload = audioData,
            RequestId = Guid.NewGuid().ToString("N")
        };
        
        if (sequence.HasValue)
        {
            message.Sequence = sequence.Value;
            message.TypeFlags |= sequence.Value > 0 ? MessageTypeFlags.PositiveSeq : MessageTypeFlags.NegativeSeq;
        }
        
        return message;
    }
    
    /// <summary>
    /// 创建错误消息
    /// </summary>
    public static ProtocolMessage CreateErrorMessage(uint errorCode, string errorMessage)
    {
        return new ProtocolMessage
        {
            Type = MessageType.Error,
            TypeFlags = MessageTypeFlags.NoSeq,
            ErrorCode = errorCode,
            Payload = System.Text.Encoding.UTF8.GetBytes(errorMessage),
            RequestId = Guid.NewGuid().ToString("N")
        };
    }
}

/// <summary>
/// 协议工具类
/// </summary>
public static class ProtocolUtils
{
    /// <summary>
    /// 检查消息是否包含序列号
    /// </summary>
    public static bool ContainsSequence(MessageTypeFlags flags)
    {
        return (flags & MessageTypeFlags.PositiveSeq) == MessageTypeFlags.PositiveSeq ||
               (flags & MessageTypeFlags.NegativeSeq) == MessageTypeFlags.NegativeSeq;
    }
    
    /// <summary>
    /// 检查消息是否包含事件
    /// </summary>
    public static bool ContainsEvent(MessageTypeFlags flags)
    {
        return (flags & MessageTypeFlags.WithEvent) == MessageTypeFlags.WithEvent;
    }
    
    /// <summary>
    /// 获取消息类型对应的字节值 (参考Go实现的msgTypeToBits)
    /// </summary>
    public static byte GetMessageTypeBits(MessageType messageType)
    {
        return messageType switch
        {
            MessageType.FullClient => 0b0001 << 4,           // 0b0001_0000
            MessageType.AudioOnlyClient => 0b0010 << 4,      // 0b0010_0000
            MessageType.FullServer => 0b1001 << 4,           // 0b1001_0000
            MessageType.AudioOnlyServer => 0b1011 << 4,      // 0b1011_0000
            MessageType.FrontEndResultServer => 0b1100 << 4, // 0b1100_0000
            MessageType.Error => 0b1111 << 4,                // 0b1111_0000
            _ => 0
        };
    }
    
    /// <summary>
    /// 从字节值获取消息类型
    /// </summary>
    public static MessageType GetMessageTypeFromBits(byte bits)
    {
        var typeBits = (byte)(bits & 0b1111_0000);
        return typeBits switch
        {
            0b0001_0000 => MessageType.FullClient,
            0b0010_0000 => MessageType.AudioOnlyClient,
            0b1001_0000 => MessageType.FullServer,
            0b1011_0000 => MessageType.AudioOnlyServer,
            0b1100_0000 => MessageType.FrontEndResultServer,
            0b1111_0000 => MessageType.Error,
            _ => MessageType.Invalid
        };
    }
}