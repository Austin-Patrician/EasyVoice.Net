using System.Text.Json.Serialization;

namespace EasyVoice.RealtimeDialog.Models.Audio;

/// <summary>
/// 音频配置
/// 符合豆包实时语音API要求
/// </summary>
public class AudioConfig
{
    /// <summary>
    /// 声道数（1=单声道，2=立体声）
    /// </summary>
    [JsonPropertyName("channels")]
    public int Channels { get; set; } = 1;
    
    /// <summary>
    /// 音频格式（pcm, pcm_s16le, ogg_opus等）
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "pcm";
    
    /// <summary>
    /// 采样率（16000, 24000等）
    /// </summary>
    [JsonPropertyName("sample_rate")]
    public int SampleRate { get; set; } = 16000;
    
    /// <summary>
    /// 位深度（16, 32等）
    /// </summary>
    [JsonPropertyName("bit_depth")]
    public int BitDepth { get; set; } = 16;
    
    /// <summary>
    /// 音频块大小
    /// </summary>
    [JsonPropertyName("chunk_size")]
    public int ChunkSize { get; set; } = 3200;
}

/// <summary>
/// 音频设备信息
/// </summary>
public class AudioDeviceInfo
{
    /// <summary>
    /// 设备ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;
    
    /// <summary>
    /// 设备名称
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为输入设备
    /// </summary>
    public bool IsInput { get; set; }
    
    /// <summary>
    /// 是否为输出设备
    /// </summary>
    public bool IsOutput { get; set; }
    
    /// <summary>
    /// 是否为默认设备
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// 支持的音频格式
    /// </summary>
    public List<AudioConfig> SupportedFormats { get; set; } = new();
}

/// <summary>
/// 音频数据包
/// </summary>
public class AudioPacket
{
    /// <summary>
    /// 音频数据
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    /// <summary>
    /// 序列号
    /// </summary>
    public uint SequenceNumber { get; set; }
    
    /// <summary>
    /// 是否为最后一个包
    /// </summary>
    public bool IsLast { get; set; }
    
    /// <summary>
    /// 音频格式
    /// </summary>
    public AudioConfig Format { get; set; } = new();
}

/// <summary>
/// 音频数据块
/// </summary>
public class AudioChunk
{
    /// <summary>
    /// 音频数据
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    /// <summary>
    /// 序列号
    /// </summary>
    public uint SequenceNumber { get; set; }
    
    /// <summary>
    /// 是否为最后一个块
    /// </summary>
    public bool IsLast { get; set; }
    
    /// <summary>
    /// 是否为空数据
    /// </summary>
    public bool IsEmpty => Data.Length == 0;
    
    /// <summary>
    /// 创建音频块
    /// </summary>
    /// <param name="data">音频数据</param>
    /// <param name="sequenceNumber">序列号</param>
    /// <param name="isLast">是否为最后一个块</param>
    /// <returns>音频块</returns>
    public static AudioChunk Create(byte[] data, uint sequenceNumber, bool isLast = false)
    {
        return new AudioChunk
        {
            Data = data,
            SequenceNumber = sequenceNumber,
            IsLast = isLast,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}