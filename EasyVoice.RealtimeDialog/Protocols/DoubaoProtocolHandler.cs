using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EasyVoice.RealtimeDialog.Models.Protocol;
using Microsoft.Extensions.Logging;

namespace EasyVoice.RealtimeDialog.Protocols;

/// <summary>
/// 豆包实时语音协议处理器
/// </summary>
public class DoubaoProtocolHandler
{
    private readonly ILogger<DoubaoProtocolHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private uint _sequenceNumber;
    
    public DoubaoProtocolHandler(ILogger<DoubaoProtocolHandler> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }
    
    /// <summary>
    /// 序列化协议消息为字节数组
    /// </summary>
    /// <param name="message">协议消息</param>
    /// <param name="compressionMethod">压缩方式</param>
    /// <param name="serializationMethod">序列化方式</param>
    /// <returns>序列化后的字节数组</returns>
    public async Task<byte[]> SerializeMessageAsync(
        ProtocolMessage message,
        CompressionMethod compressionMethod = CompressionMethod.Gzip,
        SerializationMethod serializationMethod = SerializationMethod.Json)
    {
        try
        {
            // 序列化消息体
            byte[] payload = await SerializePayloadAsync(message, serializationMethod);
            
            // 压缩消息体
            if (compressionMethod != CompressionMethod.NoCompression)
            {
                payload = await CompressDataAsync(payload, compressionMethod);
            }
            
            // 创建消息头
            var messageType = GetMessageTypeFromObject(message);
            var header = MessageHeader.Create(
                messageType,
                GetNextSequenceNumber(),
                (uint)payload.Length,
                serializationMethod,
                compressionMethod);
            
            // 计算校验和
            header.Checksum = header.CalculateChecksum(payload);
            
            // 序列化头部
            byte[] headerBytes = SerializeHeader(header);
            
            // 组合头部和消息体
            byte[] result = new byte[headerBytes.Length + payload.Length];
            Array.Copy(headerBytes, 0, result, 0, headerBytes.Length);
            Array.Copy(payload, 0, result, headerBytes.Length, payload.Length);
            
            _logger.LogDebug("序列化消息完成: Type={MessageType}, Size={Size}字节", messageType, result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "序列化消息失败: {MessageType}", message.GetType().Name);
            throw new InvalidOperationException($"序列化消息失败: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 反序列化字节数组为协议消息
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <returns>协议消息</returns>
    public async Task<ProtocolMessage> DeserializeMessageAsync(byte[] data)
    {
        try
        {
            if (data.Length < MessageHeader.HeaderSize)
            {
                throw new ArgumentException($"数据长度不足，至少需要{MessageHeader.HeaderSize}字节");
            }
            
            // 反序列化头部
            var header = DeserializeHeader(data.AsSpan(0, MessageHeader.HeaderSize));
            
            // 验证头部
            if (!header.IsValid())
            {
                throw new InvalidDataException("无效的消息头部");
            }
            
            // 提取消息体
            if (data.Length < MessageHeader.HeaderSize + header.PayloadLength)
            {
                throw new ArgumentException("数据长度不足，消息体不完整");
            }
            
            byte[] payload = data.AsSpan(MessageHeader.HeaderSize, (int)header.PayloadLength).ToArray();
            
            // 验证校验和
            if (!header.ValidateChecksum(payload))
            {
                throw new InvalidDataException("消息校验和验证失败");
            }
            
            // 解压缩消息体
            if (header.CompressionMethod != CompressionMethod.NoCompression)
            {
                payload = await DecompressDataAsync(payload, header.CompressionMethod);
            }
            
            // 反序列化消息体
            var message = await DeserializePayloadAsync(payload, header.MessageType, header.SerializationMethod);
            message.Header = header;
            
            _logger.LogDebug("反序列化消息完成: Type={MessageType}, Size={Size}字节", header.MessageType, data.Length);
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "反序列化消息失败");
            throw new InvalidOperationException($"反序列化消息失败: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 创建心跳消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="heartbeatType">心跳类型</param>
    /// <returns>心跳消息字节数组</returns>
    public async Task<byte[]> CreateHeartbeatMessageAsync(string sessionId, string heartbeatType = "ping")
    {
        var heartbeat = new HeartbeatMessage
        {
            SessionId = sessionId,
            HeartbeatType = heartbeatType,
            ClientTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        return await SerializeMessageAsync(heartbeat, CompressionMethod.NoCompression);
    }
    
    /// <summary>
    /// 创建会话控制消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="controlType">控制类型</param>
    /// <param name="sessionConfig">会话配置</param>
    /// <returns>会话控制消息字节数组</returns>
    public async Task<byte[]> CreateSessionControlMessageAsync(
        string sessionId, 
        string controlType, 
        Models.Session.SessionConfig? sessionConfig = null)
    {
        var control = new SessionControlMessage
        {
            SessionId = sessionId,
            ControlType = controlType,
            SessionConfig = sessionConfig
        };
        
        return await SerializeMessageAsync(control);
    }
    
    /// <summary>
    /// 验证消息完整性
    /// </summary>
    /// <param name="data">消息数据</param>
    /// <returns>是否完整</returns>
    public bool ValidateMessageIntegrity(ReadOnlySpan<byte> data)
    {
        try
        {
            if (data.Length < MessageHeader.HeaderSize)
            {
                return false;
            }
            
            var header = DeserializeHeader(data[..MessageHeader.HeaderSize]);
            
            if (!header.IsValid())
            {
                return false;
            }
            
            if (data.Length < MessageHeader.HeaderSize + header.PayloadLength)
            {
                return false;
            }
            
            var payload = data.Slice(MessageHeader.HeaderSize, (int)header.PayloadLength);
            return header.ValidateChecksum(payload);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取消息头部信息
    /// </summary>
    /// <param name="data">消息数据</param>
    /// <returns>消息头部</returns>
    public MessageHeader? GetMessageHeader(ReadOnlySpan<byte> data)
    {
        try
        {
            if (data.Length < MessageHeader.HeaderSize)
            {
                return null;
            }
            
            return DeserializeHeader(data[..MessageHeader.HeaderSize]);
        }
        catch
        {
            return null;
        }
    }
    
    #region Private Methods
    
    private async Task<byte[]> SerializePayloadAsync(ProtocolMessage message, SerializationMethod method)
    {
        return method switch
        {
            SerializationMethod.Json => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, message.GetType(), _jsonOptions)),
            SerializationMethod.NoSerialization => message switch
            {
                ClientAudioOnlyRequestMessage audio => Convert.FromBase64String(audio.AudioData),
                _ => throw new NotSupportedException($"无序列化方式不支持消息类型: {message.GetType().Name}")
            },
            _ => throw new NotSupportedException($"不支持的序列化方式: {method}")
        };
    }
    
    private async Task<ProtocolMessage> DeserializePayloadAsync(byte[] payload, MessageType messageType, SerializationMethod method)
    {
        return method switch
        {
            SerializationMethod.Json => DeserializeJsonMessage(payload, messageType),
            SerializationMethod.NoSerialization => DeserializeBinaryMessage(payload, messageType),
            _ => throw new NotSupportedException($"不支持的序列化方式: {method}")
        };
    }
    
    private ProtocolMessage DeserializeJsonMessage(byte[] payload, MessageType messageType)
    {
        var json = Encoding.UTF8.GetString(payload);
        
        return messageType switch
        {
            MessageType.ClientFullRequest => JsonSerializer.Deserialize<ClientFullRequestMessage>(json, _jsonOptions)!,
            MessageType.ClientAudioOnlyRequest => JsonSerializer.Deserialize<ClientAudioOnlyRequestMessage>(json, _jsonOptions)!,
            MessageType.ServerFullResponse => JsonSerializer.Deserialize<ServerFullResponseMessage>(json, _jsonOptions)!,
            MessageType.ServerAck => JsonSerializer.Deserialize<ServerAckMessage>(json, _jsonOptions)!,
            MessageType.ServerErrorResponse => JsonSerializer.Deserialize<ServerErrorResponseMessage>(json, _jsonOptions)!,
            MessageType.Heartbeat => JsonSerializer.Deserialize<HeartbeatMessage>(json, _jsonOptions)!,
            MessageType.SessionStart or MessageType.SessionEnd => JsonSerializer.Deserialize<SessionControlMessage>(json, _jsonOptions)!,
            MessageType.TtsTrigger => JsonSerializer.Deserialize<TtsTriggerMessage>(json, _jsonOptions)!,
            _ => throw new NotSupportedException($"不支持的消息类型: {messageType}")
        };
    }
    
    private ProtocolMessage DeserializeBinaryMessage(byte[] payload, MessageType messageType)
    {
        return messageType switch
        {
            MessageType.ClientAudioOnlyRequest => new ClientAudioOnlyRequestMessage
            {
                AudioData = Convert.ToBase64String(payload),
                AudioFormat = Models.Protocol.AudioFormat.CreateInputFormat()
            },
            _ => throw new NotSupportedException($"二进制反序列化不支持消息类型: {messageType}")
        };
    }
    
    private async Task<byte[]> CompressDataAsync(byte[] data, CompressionMethod method)
    {
        return method switch
        {
            CompressionMethod.Gzip => await CompressGzipAsync(data),
            CompressionMethod.NoCompression => data,
            _ => throw new NotSupportedException($"不支持的压缩方式: {method}")
        };
    }
    
    private async Task<byte[]> DecompressDataAsync(byte[] data, CompressionMethod method)
    {
        return method switch
        {
            CompressionMethod.Gzip => await DecompressGzipAsync(data),
            CompressionMethod.NoCompression => data,
            _ => throw new NotSupportedException($"不支持的压缩方式: {method}")
        };
    }
    
    private async Task<byte[]> CompressGzipAsync(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            await gzip.WriteAsync(data);
        }
        return output.ToArray();
    }
    
    private async Task<byte[]> DecompressGzipAsync(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output);
        return output.ToArray();
    }
    
    private byte[] SerializeHeader(MessageHeader header)
    {
        var bytes = new byte[MessageHeader.HeaderSize];
        var span = bytes.AsSpan();
        
        MemoryMarshal.Write(span, in header);
        return bytes;
    }
    
    private MessageHeader DeserializeHeader(ReadOnlySpan<byte> data)
    {
        return MemoryMarshal.Read<MessageHeader>(data);
    }
    
    private MessageType GetMessageTypeFromObject(ProtocolMessage message)
    {
        return message switch
        {
            ClientFullRequestMessage => MessageType.ClientFullRequest,
            ClientAudioOnlyRequestMessage => MessageType.ClientAudioOnlyRequest,
            ServerFullResponseMessage => MessageType.ServerFullResponse,
            ServerAckMessage => MessageType.ServerAck,
            ServerErrorResponseMessage => MessageType.ServerErrorResponse,
            HeartbeatMessage => MessageType.Heartbeat,
            SessionControlMessage control => control.ControlType == "start" ? MessageType.SessionStart : MessageType.SessionEnd,
            TtsTriggerMessage => MessageType.TtsTrigger,
            _ => throw new NotSupportedException($"不支持的消息类型: {message.GetType().Name}")
        };
    }
    
    private uint GetNextSequenceNumber()
    {
        return Interlocked.Increment(ref _sequenceNumber);
    }
    
    #endregion
}