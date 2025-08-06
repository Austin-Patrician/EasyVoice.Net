using System.Runtime.InteropServices;

namespace EasyVoice.RealtimeDialog.Models.Protocol;

/// <summary>
/// 豆包实时语音协议消息头部
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MessageHeader
{
    /// <summary>
    /// 协议版本号
    /// </summary>
    public byte Version { get; set; }
    
    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageType MessageType { get; set; }
    
    /// <summary>
    /// 序列化方法
    /// </summary>
    public SerializationMethod SerializationMethod { get; set; }
    
    /// <summary>
    /// 压缩方式
    /// </summary>
    public CompressionMethod CompressionMethod { get; set; }
    
    /// <summary>
    /// 消息标志
    /// </summary>
    public MessageFlags Flags { get; set; }
    
    /// <summary>
    /// 序列号
    /// </summary>
    public uint SequenceNumber { get; set; }
    
    /// <summary>
    /// 时间戳（Unix毫秒）
    /// </summary>
    public long Timestamp { get; set; }
    
    /// <summary>
    /// 消息体长度
    /// </summary>
    public uint PayloadLength { get; set; }
    
    /// <summary>
    /// 校验和
    /// </summary>
    public uint Checksum { get; set; }
    
    /// <summary>
    /// 消息头部固定大小（字节）
    /// </summary>
    public const int HeaderSize = 25;
    
    /// <summary>
    /// 当前协议版本
    /// </summary>
    public const byte CurrentVersion = 0x01;
    
    /// <summary>
    /// 创建新的消息头部
    /// </summary>
    /// <param name="messageType">消息类型</param>
    /// <param name="sequenceNumber">序列号</param>
    /// <param name="payloadLength">消息体长度</param>
    /// <param name="serializationMethod">序列化方法</param>
    /// <param name="compressionMethod">压缩方式</param>
    /// <param name="flags">消息标志</param>
    /// <returns>消息头部</returns>
    public static MessageHeader Create(
        MessageType messageType,
        uint sequenceNumber,
        uint payloadLength,
        SerializationMethod serializationMethod = SerializationMethod.Json,
        CompressionMethod compressionMethod = CompressionMethod.Gzip,
        MessageFlags flags = MessageFlags.None)
    {
        return new MessageHeader
        {
            Version = CurrentVersion,
            MessageType = messageType,
            SerializationMethod = serializationMethod,
            CompressionMethod = compressionMethod,
            Flags = flags,
            SequenceNumber = sequenceNumber,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PayloadLength = payloadLength,
            Checksum = 0 // 将在序列化时计算
        };
    }
    
    /// <summary>
    /// 验证消息头部
    /// </summary>
    /// <returns>是否有效</returns>
    public readonly bool IsValid()
    {
        return Version == CurrentVersion && PayloadLength > 0;
    }
    
    /// <summary>
    /// 计算校验和
    /// </summary>
    /// <param name="payload">消息体数据</param>
    /// <returns>校验和</returns>
    public uint CalculateChecksum(ReadOnlySpan<byte> payload)
    {
        // 简单的CRC32校验和实现
        uint crc = 0xFFFFFFFF;
        
        // 计算头部数据的校验和（除了校验和字段本身）
        var headerBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref this, 1));
        var headerWithoutChecksum = headerBytes[..^4]; // 排除最后4字节的校验和字段
        
        foreach (byte b in headerWithoutChecksum)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        
        // 计算消息体的校验和
        foreach (byte b in payload)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        
        return ~crc;
    }
    
    /// <summary>
    /// 验证校验和
    /// </summary>
    /// <param name="payload">消息体数据</param>
    /// <returns>校验和是否正确</returns>
    public readonly bool ValidateChecksum(ReadOnlySpan<byte> payload)
    {
        var header = this;
        var expectedChecksum = header.CalculateChecksum(payload);
        return Checksum == expectedChecksum;
    }
}