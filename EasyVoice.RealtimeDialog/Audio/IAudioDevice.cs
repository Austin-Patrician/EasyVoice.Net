using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Models.Protocol;

namespace EasyVoice.RealtimeDialog.Audio;

/// <summary>
/// 音频设备接口
/// </summary>
public interface IAudioDevice : IDisposable
{
    /// <summary>
    /// 设备信息
    /// </summary>
    AudioDeviceInfo DeviceInfo { get; }
    
    /// <summary>
    /// 是否正在录制
    /// </summary>
    bool IsRecording { get; }
    
    /// <summary>
    /// 是否正在播放
    /// </summary>
    bool IsPlaying { get; }
    
    /// <summary>
    /// 音频统计信息
    /// </summary>
    AudioStatistics Statistics { get; }
    
    /// <summary>
    /// 初始化音频设备
    /// </summary>
    /// <param name="config">音频配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化任务</returns>
    Task InitializeAsync(AudioConfig config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 开始录制音频
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>录制任务</returns>
    Task StartRecordingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止录制音频
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止任务</returns>
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 播放音频数据
    /// </summary>
    /// <param name="audioChunk">音频块</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>播放任务</returns>
    Task PlayAudioAsync(AudioChunk audioChunk, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止播放音频
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止任务</returns>
    Task StopPlaybackAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 设置输入音量
    /// </summary>
    /// <param name="volume">音量（0.0-1.0）</param>
    void SetInputVolume(float volume);
    
    /// <summary>
    /// 设置输出音量
    /// </summary>
    /// <param name="volume">音量（0.0-1.0）</param>
    void SetOutputVolume(float volume);
    
    /// <summary>
    /// 获取可用的输入设备列表
    /// </summary>
    /// <returns>设备列表</returns>
    Task<IEnumerable<AudioDeviceInfo>> GetInputDevicesAsync();
    
    /// <summary>
    /// 获取可用的输出设备列表
    /// </summary>
    /// <returns>设备列表</returns>
    Task<IEnumerable<AudioDeviceInfo>> GetOutputDevicesAsync();
    
    /// <summary>
    /// 切换输入设备
    /// </summary>
    /// <param name="deviceId">设备ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切换任务</returns>
    Task SwitchInputDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 切换输出设备
    /// </summary>
    /// <param name="deviceId">设备ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切换任务</returns>
    Task SwitchOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 音频数据可用事件
    /// </summary>
    event EventHandler<AudioDataAvailableEventArgs> AudioDataAvailable;
    
    /// <summary>
    /// 播放完成事件
    /// </summary>
    event EventHandler<PlaybackCompletedEventArgs> PlaybackCompleted;
    
    /// <summary>
    /// 设备错误事件
    /// </summary>
    event EventHandler<AudioDeviceErrorEventArgs> DeviceError;
    
    /// <summary>
    /// 设备状态变化事件
    /// </summary>
    event EventHandler<DeviceStateChangedEventArgs> DeviceStateChanged;
}

/// <summary>
/// 音频数据可用事件参数
/// </summary>
public class AudioDataAvailableEventArgs : EventArgs
{
    /// <summary>
    /// 音频块
    /// </summary>
    public AudioChunk AudioChunk { get; set; } = null!;
    
    /// <summary>
    /// 录制时间戳
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 音频级别（0.0-1.0）
    /// </summary>
    public float AudioLevel { get; set; }
    
    /// <summary>
    /// 是否检测到静音
    /// </summary>
    public bool IsSilence { get; set; }
}

/// <summary>
/// 播放完成事件参数
/// </summary>
public class PlaybackCompletedEventArgs : EventArgs
{
    /// <summary>
    /// 播放的音频块
    /// </summary>
    public AudioChunk AudioChunk { get; set; } = null!;
    
    /// <summary>
    /// 播放持续时间（毫秒）
    /// </summary>
    public double PlaybackDurationMs { get; set; }
    
    /// <summary>
    /// 是否成功播放
    /// </summary>
    public bool IsSuccessful { get; set; } = true;
    
    /// <summary>
    /// 错误信息（如果播放失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 音频设备错误事件参数
/// </summary>
public class AudioDeviceErrorEventArgs : EventArgs
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public AudioErrorType ErrorType { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// 是否可恢复
    /// </summary>
    public bool IsRecoverable { get; set; }
    
    /// <summary>
    /// 建议的恢复操作
    /// </summary>
    public string? RecoveryAction { get; set; }
}

/// <summary>
/// 设备状态变化事件参数
/// </summary>
public class DeviceStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 设备ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;
    
    /// <summary>
    /// 旧状态
    /// </summary>
    public AudioDeviceState OldState { get; set; }
    
    /// <summary>
    /// 新状态
    /// </summary>
    public AudioDeviceState NewState { get; set; }
    
    /// <summary>
    /// 状态变化时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 音频错误类型
/// </summary>
public enum AudioErrorType
{
    /// <summary>
    /// 设备未找到
    /// </summary>
    DeviceNotFound,
    
    /// <summary>
    /// 设备正在使用
    /// </summary>
    DeviceInUse,
    
    /// <summary>
    /// 格式不支持
    /// </summary>
    FormatNotSupported,
    
    /// <summary>
    /// 缓冲区溢出
    /// </summary>
    BufferOverflow,
    
    /// <summary>
    /// 缓冲区欠载
    /// </summary>
    BufferUnderflow,
    
    /// <summary>
    /// 权限不足
    /// </summary>
    PermissionDenied,
    
    /// <summary>
    /// 驱动程序错误
    /// </summary>
    DriverError,
    
    /// <summary>
    /// 硬件错误
    /// </summary>
    HardwareError,
    
    /// <summary>
    /// 网络错误
    /// </summary>
    NetworkError,
    
    /// <summary>
    /// 未知错误
    /// </summary>
    UnknownError
}