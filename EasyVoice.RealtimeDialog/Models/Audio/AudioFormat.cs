using System.Text.Json.Serialization;

namespace EasyVoice.RealtimeDialog.Models.Protocol;

/// <summary>
/// 音频格式定义
/// </summary>
public class AudioFormat
{
    /// <summary>
    /// 采样率（Hz）
    /// </summary>
    [JsonPropertyName("sample_rate")]
    public int SampleRate { get; set; } = 16000;
    
    /// <summary>
    /// 声道数
    /// </summary>
    [JsonPropertyName("channels")]
    public int Channels { get; set; } = 1;
    
    /// <summary>
    /// 位深度
    /// </summary>
    [JsonPropertyName("bits_per_sample")]
    public int BitsPerSample { get; set; } = 16;
    
    /// <summary>
    /// 音频编码格式
    /// </summary>
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "pcm";
    
    /// <summary>
    /// 字节序（little-endian/big-endian）
    /// </summary>
    [JsonPropertyName("endianness")]
    public string Endianness { get; set; } = "little-endian";
    
    /// <summary>
    /// 每秒字节数
    /// </summary>
    [JsonIgnore]
    public int BytesPerSecond => SampleRate * Channels * (BitsPerSample / 8);
    
    /// <summary>
    /// 每个样本的字节数
    /// </summary>
    [JsonIgnore]
    public int BytesPerSample => Channels * (BitsPerSample / 8);
    
    /// <summary>
    /// 创建默认的输入音频格式（16kHz PCM）
    /// </summary>
    /// <returns>音频格式</returns>
    public static AudioFormat CreateInputFormat()
    {
        return new AudioFormat
        {
            SampleRate = 16000,
            Channels = 1,
            BitsPerSample = 16,
            Encoding = "pcm",
            Endianness = "little-endian"
        };
    }
    
    /// <summary>
    /// 创建默认的输出音频格式（24kHz PCM）
    /// </summary>
    /// <returns>音频格式</returns>
    public static AudioFormat CreateOutputFormat()
    {
        return new AudioFormat
        {
            SampleRate = 24000,
            Channels = 1,
            BitsPerSample = 16,
            Encoding = "pcm",
            Endianness = "little-endian"
        };
    }
    
    /// <summary>
    /// 验证音频格式是否有效
    /// </summary>
    /// <returns>是否有效</returns>
    public bool IsValid()
    {
        return SampleRate > 0 && 
               Channels > 0 && 
               BitsPerSample > 0 && 
               !string.IsNullOrEmpty(Encoding);
    }
    
    /// <summary>
    /// 检查是否与另一个格式兼容
    /// </summary>
    /// <param name="other">另一个音频格式</param>
    /// <returns>是否兼容</returns>
    public bool IsCompatibleWith(AudioFormat other)
    {
        return SampleRate == other.SampleRate &&
               Channels == other.Channels &&
               BitsPerSample == other.BitsPerSample &&
               Encoding.Equals(other.Encoding, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 计算音频数据的持续时间
    /// </summary>
    /// <param name="audioDataLength">音频数据长度（字节）</param>
    /// <returns>持续时间（毫秒）</returns>
    public double CalculateDurationMs(int audioDataLength)
    {
        if (BytesPerSecond == 0) return 0;
        return (double)audioDataLength / BytesPerSecond * 1000;
    }
    
    /// <summary>
    /// 计算指定持续时间需要的字节数
    /// </summary>
    /// <param name="durationMs">持续时间（毫秒）</param>
    /// <returns>字节数</returns>
    public int CalculateBytes(double durationMs)
    {
        return (int)(BytesPerSecond * durationMs / 1000);
    }
    
    public override string ToString()
    {
        return $"{SampleRate}Hz_{Channels}ch_{BitsPerSample}bit_{Encoding}";
    }
    
    public override bool Equals(object? obj)
    {
        return obj is AudioFormat other && IsCompatibleWith(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(SampleRate, Channels, BitsPerSample, Encoding.ToLowerInvariant());
    }
}