using System.Text.Json.Serialization;

namespace EasyVoice.RealtimeDialog.Models.Audio;

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
    /// 音频格式
    /// </summary>
    public Protocol.AudioFormat Format { get; set; } = new();
    
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
    /// 持续时间（毫秒）
    /// </summary>
    public double DurationMs => Format.CalculateDurationMs(Data.Length);
    
    /// <summary>
    /// 是否为空数据
    /// </summary>
    public bool IsEmpty => Data.Length == 0;
    
    /// <summary>
    /// 创建音频块
    /// </summary>
    /// <param name="data">音频数据</param>
    /// <param name="format">音频格式</param>
    /// <param name="sequenceNumber">序列号</param>
    /// <param name="isLast">是否为最后一个块</param>
    /// <returns>音频块</returns>
    public static AudioChunk Create(byte[] data, Protocol.AudioFormat format, uint sequenceNumber, bool isLast = false)
    {
        return new AudioChunk
        {
            Data = data,
            Format = format,
            SequenceNumber = sequenceNumber,
            IsLast = isLast,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
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
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 设备类型
    /// </summary>
    public AudioDeviceType Type { get; set; }
    
    /// <summary>
    /// 是否为默认设备
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// 支持的采样率
    /// </summary>
    public int[] SupportedSampleRates { get; set; } = Array.Empty<int>();
    
    /// <summary>
    /// 支持的声道数
    /// </summary>
    public int[] SupportedChannels { get; set; } = Array.Empty<int>();
    
    /// <summary>
    /// 设备状态
    /// </summary>
    public AudioDeviceState State { get; set; } = AudioDeviceState.Unknown;
    
    /// <summary>
    /// 驱动程序名称
    /// </summary>
    public string? DriverName { get; set; }
    
    /// <summary>
    /// 设备描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 音频设备类型
/// </summary>
public enum AudioDeviceType
{
    /// <summary>
    /// 输入设备（麦克风）
    /// </summary>
    Input,
    
    /// <summary>
    /// 输出设备（扬声器）
    /// </summary>
    Output,
    
    /// <summary>
    /// 双向设备
    /// </summary>
    Duplex
}

/// <summary>
/// 音频设备状态
/// </summary>
public enum AudioDeviceState
{
    /// <summary>
    /// 未知状态
    /// </summary>
    Unknown,
    
    /// <summary>
    /// 活动状态
    /// </summary>
    Active,
    
    /// <summary>
    /// 禁用状态
    /// </summary>
    Disabled,
    
    /// <summary>
    /// 未连接
    /// </summary>
    NotPresent,
    
    /// <summary>
    /// 未插入
    /// </summary>
    Unplugged
}

/// <summary>
/// 音频配置
/// </summary>
public class AudioConfig
{
    /// <summary>
    /// 输入设备ID
    /// </summary>
    [JsonPropertyName("input_device_id")]
    public string? InputDeviceId { get; set; }
    
    /// <summary>
    /// 输出设备ID
    /// </summary>
    [JsonPropertyName("output_device_id")]
    public string? OutputDeviceId { get; set; }
    
    /// <summary>
    /// 输入音频格式
    /// </summary>
    [JsonPropertyName("input_format")]
    public Protocol.AudioFormat InputFormat { get; set; } = Protocol.AudioFormat.CreateInputFormat();
    
    /// <summary>
    /// 输出音频格式
    /// </summary>
    [JsonPropertyName("output_format")]
    public Protocol.AudioFormat OutputFormat { get; set; } = Protocol.AudioFormat.CreateOutputFormat();
    
    /// <summary>
    /// 缓冲区大小（毫秒）
    /// </summary>
    [JsonPropertyName("buffer_size_ms")]
    public int BufferSizeMs { get; set; } = 100;
    
    /// <summary>
    /// 输入音量（0.0-1.0）
    /// </summary>
    [JsonPropertyName("input_volume")]
    public float InputVolume { get; set; } = 1.0f;
    
    /// <summary>
    /// 输出音量（0.0-1.0）
    /// </summary>
    [JsonPropertyName("output_volume")]
    public float OutputVolume { get; set; } = 1.0f;
    
    /// <summary>
    /// 是否启用噪声抑制
    /// </summary>
    [JsonPropertyName("noise_suppression")]
    public bool NoiseSuppressionEnabled { get; set; } = true;
    
    /// <summary>
    /// 是否启用回声消除
    /// </summary>
    [JsonPropertyName("echo_cancellation")]
    public bool EchoCancellationEnabled { get; set; } = true;
    
    /// <summary>
    /// 是否启用自动增益控制
    /// </summary>
    [JsonPropertyName("auto_gain_control")]
    public bool AutoGainControlEnabled { get; set; } = true;
    
    /// <summary>
    /// 静音检测阈值
    /// </summary>
    [JsonPropertyName("silence_threshold")]
    public float SilenceThreshold { get; set; } = 0.01f;
    
    /// <summary>
    /// 静音检测持续时间（毫秒）
    /// </summary>
    [JsonPropertyName("silence_duration_ms")]
    public int SilenceDurationMs { get; set; } = 1000;
}

/// <summary>
/// 音频统计信息
/// </summary>
public class AudioStatistics
{
    /// <summary>
    /// 录制的音频块数量
    /// </summary>
    public long RecordedChunks { get; set; }
    
    /// <summary>
    /// 播放的音频块数量
    /// </summary>
    public long PlayedChunks { get; set; }
    
    /// <summary>
    /// 丢失的音频块数量
    /// </summary>
    public long DroppedChunks { get; set; }
    
    /// <summary>
    /// 总录制时长（毫秒）
    /// </summary>
    public long TotalRecordingTimeMs { get; set; }
    
    /// <summary>
    /// 总播放时长（毫秒）
    /// </summary>
    public long TotalPlaybackTimeMs { get; set; }
    
    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public double AverageLatencyMs { get; set; }
    
    /// <summary>
    /// 最大延迟（毫秒）
    /// </summary>
    public double MaxLatencyMs { get; set; }
    
    /// <summary>
    /// 缓冲区溢出次数
    /// </summary>
    public long BufferOverflows { get; set; }
    
    /// <summary>
    /// 缓冲区欠载次数
    /// </summary>
    public long BufferUnderflows { get; set; }
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 计算丢包率
    /// </summary>
    /// <returns>丢包率（0.0-1.0）</returns>
    public double CalculateDropRate()
    {
        var totalChunks = RecordedChunks + DroppedChunks;
        return totalChunks > 0 ? (double)DroppedChunks / totalChunks : 0.0;
    }
    
    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void Reset()
    {
        RecordedChunks = 0;
        PlayedChunks = 0;
        DroppedChunks = 0;
        TotalRecordingTimeMs = 0;
        TotalPlaybackTimeMs = 0;
        AverageLatencyMs = 0;
        MaxLatencyMs = 0;
        BufferOverflows = 0;
        BufferUnderflows = 0;
        LastUpdated = DateTime.UtcNow;
    }
}