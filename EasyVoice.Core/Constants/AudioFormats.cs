namespace EasyVoice.Core.Constants;

/// <summary>
/// 音频格式枚举
/// 定义支持的音频输出格式
/// </summary>
public enum AudioFormat
{
    /// <summary>
    /// MP3 格式 - 压缩音频格式，文件小，兼容性好
    /// </summary>
    Mp3 = 1,

    /// <summary>
    /// WAV 格式 - 无损音频格式，音质高，文件大
    /// </summary>
    Wav = 2,

    /// <summary>
    /// OGG 格式 - 开源压缩音频格式
    /// </summary>
    Ogg = 3,

    /// <summary>
    /// FLAC 格式 - 无损压缩音频格式
    /// </summary>
    Flac = 4,

    /// <summary>
    /// PCM 格式 - 原始音频数据格式
    /// </summary>
    Pcm = 5,

    /// <summary>
    /// Opus 格式 - 低延迟音频编码格式
    /// </summary>
    Opus = 6
}

/// <summary>
/// 音频格式扩展方法
/// </summary>
public static class AudioFormatExtensions
{
    /// <summary>
    /// 获取音频格式的 MIME 类型
    /// </summary>
    /// <param name="format">音频格式</param>
    /// <returns>MIME 类型字符串</returns>
    public static string GetMimeType(this AudioFormat format)
    {
        return format switch
        {
            AudioFormat.Mp3 => "audio/mpeg",
            AudioFormat.Wav => "audio/wav",
            AudioFormat.Ogg => "audio/ogg",
            AudioFormat.Flac => "audio/flac",
            AudioFormat.Pcm => "audio/pcm",
            AudioFormat.Opus => "audio/opus",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// 获取音频格式的文件扩展名
    /// </summary>
    /// <param name="format">音频格式</param>
    /// <returns>文件扩展名（不包含点号）</returns>
    public static string GetFileExtension(this AudioFormat format)
    {
        return format switch
        {
            AudioFormat.Mp3 => "mp3",
            AudioFormat.Wav => "wav",
            AudioFormat.Ogg => "ogg",
            AudioFormat.Flac => "flac",
            AudioFormat.Pcm => "pcm",
            AudioFormat.Opus => "opus",
            _ => "bin"
        };
    }

    /// <summary>
    /// 从字符串解析音频格式
    /// </summary>
    /// <param name="formatString">格式字符串</param>
    /// <returns>音频格式枚举值</returns>
    public static AudioFormat FromString(string formatString)
    {
        return formatString?.ToLowerInvariant() switch
        {
            "mp3" or "audio/mpeg" => AudioFormat.Mp3,
            "wav" or "audio/wav" => AudioFormat.Wav,
            "ogg" or "audio/ogg" => AudioFormat.Ogg,
            "flac" or "audio/flac" => AudioFormat.Flac,
            "pcm" or "audio/pcm" => AudioFormat.Pcm,
            "opus" or "audio/opus" => AudioFormat.Opus,
            _ => AudioFormat.Mp3 // 默认返回 MP3
        };
    }
}
