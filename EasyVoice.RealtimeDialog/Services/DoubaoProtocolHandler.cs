using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EasyVoice.RealtimeDialog.Models.Protocol;
using Microsoft.Extensions.Logging;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// 豆包实时语音协议处理器
/// 实现完整的WebSocket二进制协议，支持server_vad模式
/// </summary>
public class DoubaoProtocolHandler
{
    private readonly ILogger<DoubaoProtocolHandler> _logger;
    
    // 协议常量
    private const byte PROTOCOL_VERSION = 0x01;
    private const byte DEFAULT_HEADER_SIZE = 0x01;
    
    // 消息类型
    private const byte CLIENT_FULL_REQUEST = 0x01;
    private const byte CLIENT_AUDIO_ONLY_REQUEST = 0x02;
    private const byte SERVER_FULL_RESPONSE = 0x09;
    private const byte SERVER_ACK = 0x0B;
    private const byte SERVER_ERROR_RESPONSE = 0x0F;
    
    // 消息标志
    private const byte NO_SEQUENCE = 0x00;
    private const byte POS_SEQUENCE = 0x01;
    private const byte NEG_SEQUENCE = 0x02;
    private const byte MSG_WITH_EVENT = 0x04;
    
    // 序列化方法
    private const byte NO_SERIALIZATION = 0x00;
    private const byte JSON = 0x01;
    
    // 压缩方法
    private const byte NO_COMPRESSION = 0x00;
    private const byte GZIP = 0x01;
    
    // 事件类型
    public const int START_CONNECTION = 1;
    public const int FINISH_CONNECTION = 2;
    public const int START_SESSION = 100;
    public const int SESSION_STARTED = 101;
    public const int FINISH_SESSION = 102;
    public const int TASK_REQUEST = 200;
    public const int SAY_HELLO = 300;
    public const int TTS_RESPONSE = 350;
    public const int ASR_INFO = 450;
    public const int ASR_RESPONSE = 451;
    public const int ASR_ENDED = 459;
    public const int CHAT_TTS_TEXT = 500;
    
    public DoubaoProtocolHandler(ILogger<DoubaoProtocolHandler> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 生成协议头部
    /// </summary>
    public byte[] GenerateHeader(
        byte messageType = CLIENT_FULL_REQUEST,
        byte messageTypeSpecificFlags = MSG_WITH_EVENT,
        byte serialMethod = JSON,
        byte compressionType = GZIP,
        byte reservedData = 0x00,
        byte[] extensionHeader = null)
    {
        extensionHeader ??= Array.Empty<byte>();
        
        var header = new List<byte>();
        var headerSize = (byte)(extensionHeader.Length / 4 + 1);
        
        header.Add((byte)((PROTOCOL_VERSION << 4) | headerSize));
        header.Add((byte)((messageType << 4) | messageTypeSpecificFlags));
        header.Add((byte)((serialMethod << 4) | compressionType));
        header.Add(reservedData);
        header.AddRange(extensionHeader);
        
        return header.ToArray();
    }
    
    /// <summary>
    /// 创建StartConnection请求
    /// </summary>
    public byte[] CreateStartConnectionRequest()
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader());
        request.AddRange(BitConverter.GetBytes(START_CONNECTION).Reverse()); // Big-endian
        
        var payload = "{}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var compressedPayload = Compress(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(compressedPayload.Length).Reverse());
        request.AddRange(compressedPayload);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 创建StartSession请求
    /// </summary>
    public byte[] CreateStartSessionRequest(string sessionId, object sessionConfig)
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader());
        request.AddRange(BitConverter.GetBytes(START_SESSION).Reverse());
        
        var sessionIdBytes = Encoding.UTF8.GetBytes(sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        var payload = JsonSerializer.Serialize(sessionConfig);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var compressedPayload = Compress(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(compressedPayload.Length).Reverse());
        request.AddRange(compressedPayload);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 创建TaskRequest（音频数据）
    /// </summary>
    public byte[] CreateTaskRequest(string sessionId, byte[] audioData)
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader(
            messageType: CLIENT_AUDIO_ONLY_REQUEST,
            serialMethod: NO_SERIALIZATION));
        request.AddRange(BitConverter.GetBytes(TASK_REQUEST).Reverse());
        
        var sessionIdBytes = Encoding.UTF8.GetBytes(sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        var compressedAudio = Compress(audioData);
        request.AddRange(BitConverter.GetBytes(compressedAudio.Length).Reverse());
        request.AddRange(compressedAudio);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 创建Hello请求（事件300）
    /// </summary>
    public byte[] CreateHelloRequest()
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader());
        request.AddRange(BitConverter.GetBytes(300).Reverse()); // Hello事件
        
        var payload = new
        {
            content = "你好，我是豆包，有什么可以帮助你的？"
        };
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        
        // 压缩payload
        using var compressedStream = new MemoryStream();
        using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
        {
            gzipStream.Write(payloadBytes, 0, payloadBytes.Length);
        }
        var compressedPayload = compressedStream.ToArray();
        
        // 添加session_id长度和内容（空字符串）
        request.AddRange(BitConverter.GetBytes(0).Reverse());
        
        // 添加payload长度和内容
        request.AddRange(BitConverter.GetBytes(compressedPayload.Length).Reverse());
        request.AddRange(compressedPayload);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 创建ChatTTSText请求（事件500）
    /// </summary>
    public byte[] CreateChatTtsTextRequest(string sessionId, string content, bool start, bool end)
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader());
        request.AddRange(BitConverter.GetBytes(CHAT_TTS_TEXT).Reverse());
        
        var sessionIdBytes = Encoding.UTF8.GetBytes(sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        var payload = new
        {
            start = start,
            end = end,
            content = content
        };
        
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var compressedPayload = Compress(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(compressedPayload.Length).Reverse());
        request.AddRange(compressedPayload);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 创建FinishSession请求
    /// </summary>
    public byte[] CreateFinishSessionRequest(string sessionId)
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader());
        request.AddRange(BitConverter.GetBytes(FINISH_SESSION).Reverse());
        
        var sessionIdBytes = Encoding.UTF8.GetBytes(sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        var payload = "{}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var compressedPayload = Compress(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(compressedPayload.Length).Reverse());
        request.AddRange(compressedPayload);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 创建FinishConnection请求
    /// </summary>
    public byte[] CreateFinishConnectionRequest()
    {
        var request = new List<byte>();
        request.AddRange(GenerateHeader());
        request.AddRange(BitConverter.GetBytes(FINISH_CONNECTION).Reverse());
        
        var payload = "{}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var compressedPayload = Compress(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(compressedPayload.Length).Reverse());
        request.AddRange(compressedPayload);
        
        return request.ToArray();
    }
    
    /// <summary>
    /// 解析服务器响应
    /// </summary>
    public DoubaoResponse ParseResponse(byte[] response)
    {
        if (response == null || response.Length < 4)
        {
            return new DoubaoResponse { MessageType = "INVALID" };
        }
        
        try
        {
            var protocolVersion = (byte)(response[0] >> 4);
            var headerSize = (byte)(response[0] & 0x0F);
            var messageType = (byte)(response[1] >> 4);
            var messageTypeSpecificFlags = (byte)(response[1] & 0x0F);
            var serializationMethod = (byte)(response[2] >> 4);
            var messageCompression = (byte)(response[2] & 0x0F);
            var reserved = response[3];
            
            var headerExtensions = response.Skip(4).Take(headerSize * 4 - 4).ToArray();
            var payload = response.Skip(headerSize * 4).ToArray();
            
            var result = new DoubaoResponse();
            byte[] payloadMsg = null;
            var payloadSize = 0;
            var start = 0;
            
            if (messageType == SERVER_FULL_RESPONSE || messageType == SERVER_ACK)
            {
                result.MessageType = messageType == SERVER_ACK ? "SERVER_ACK" : "SERVER_FULL_RESPONSE";
                
                if ((messageTypeSpecificFlags & NEG_SEQUENCE) > 0)
                {
                    result.Sequence = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
                    start += 4;
                }
                
                if ((messageTypeSpecificFlags & MSG_WITH_EVENT) > 0)
                {
                    result.Event = BitConverter.ToInt32(payload.Skip(start).Take(4).Reverse().ToArray(), 0);
                    start += 4;
                }
                
                payload = payload.Skip(start).ToArray();
                var sessionIdSize = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
                var sessionId = payload.Skip(4).Take(sessionIdSize).ToArray();
                result.SessionId = Encoding.UTF8.GetString(sessionId);
                
                payload = payload.Skip(4 + sessionIdSize).ToArray();
                payloadSize = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
                payloadMsg = payload.Skip(4).ToArray();
            }
            else if (messageType == SERVER_ERROR_RESPONSE)
            {
                result.MessageType = "SERVER_ERROR";
                var code = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
                result.Code = code;
                payloadSize = BitConverter.ToInt32(payload.Skip(4).Take(4).Reverse().ToArray(), 0);
                payloadMsg = payload.Skip(8).ToArray();
            }
            
            if (payloadMsg != null)
            {
                if (messageCompression == GZIP)
                {
                    payloadMsg = Decompress(payloadMsg);
                }
                
                if (serializationMethod == JSON)
                {
                    var jsonString = Encoding.UTF8.GetString(payloadMsg);
                    result.PayloadMsg = JsonSerializer.Deserialize<object>(jsonString);
                }
                else if (serializationMethod != NO_SERIALIZATION)
                {
                    result.PayloadMsg = Encoding.UTF8.GetString(payloadMsg);
                }
                else
                {
                    result.PayloadMsg = payloadMsg; // 原始字节数据（如音频）
                }
            }
            
            result.PayloadSize = payloadSize;
            
            // 填充Header和Payload属性以保持兼容性
            result.Header = new DoubaoResponseHeader
            {
                SessionId = result.SessionId,
                Event = result.Event,
                Sequence = result.Sequence,
                MessageType = result.MessageType
            };
            result.Payload = result.PayloadMsg;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析响应失败");
            return new DoubaoResponse { MessageType = "ERROR", PayloadMsg = ex.Message };
        }
    }
    
    /// <summary>
    /// GZIP压缩
    /// </summary>
    private byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }
    
    /// <summary>
    /// GZIP解压缩
    /// </summary>
    private byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        {
            gzip.CopyTo(output);
        }
        return output.ToArray();
    }
}

/// <summary>
/// 豆包响应数据模型
/// </summary>
public class DoubaoResponse
{
    public string MessageType { get; set; } = string.Empty;
    public int? Sequence { get; set; }
    public int? Event { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public object? PayloadMsg { get; set; }
    public int PayloadSize { get; set; }
    public int? Code { get; set; }
    
    /// <summary>
    /// 响应头信息
    /// </summary>
    public DoubaoResponseHeader? Header { get; set; }
    
    /// <summary>
    /// 响应载荷
    /// </summary>
    public object? Payload { get; set; }
}

/// <summary>
/// 豆包响应头信息
/// </summary>
public class DoubaoResponseHeader
{
    public string SessionId { get; set; } = string.Empty;
    public int? Event { get; set; }
    public int? Sequence { get; set; }
    public string MessageType { get; set; } = string.Empty;
}