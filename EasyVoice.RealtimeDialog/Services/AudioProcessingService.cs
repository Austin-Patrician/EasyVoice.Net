using System.Collections.Concurrent;
using System.Threading.Channels;
using EasyVoice.RealtimeDialog.Audio;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Models.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// 音频处理服务
/// </summary>
public class AudioProcessingService : IDisposable
{
    private readonly ILogger<AudioProcessingService> _logger;
    private readonly IAudioDevice _audioDevice;
    private readonly RealtimeDialogOptions _options;
    
    private readonly Channel<AudioChunk> _inputAudioChannel;
    private readonly Channel<AudioChunk> _outputAudioChannel;
    private readonly ChannelWriter<AudioChunk> _inputWriter;
    private readonly ChannelReader<AudioChunk> _inputReader;
    private readonly ChannelWriter<AudioChunk> _outputWriter;
    private readonly ChannelReader<AudioChunk> _outputReader;
    
    private readonly ConcurrentDictionary<string, AudioSession> _audioSessions;
    private readonly Timer _statisticsTimer;
    private readonly SemaphoreSlim _deviceLock;
    
    private CancellationTokenSource? _processingCancellation;
    private Task? _inputProcessingTask;
    private Task? _outputProcessingTask;
    private bool _disposed;
    
    public AudioProcessingService(
        ILogger<AudioProcessingService> logger,
        IAudioDevice audioDevice,
        IOptions<RealtimeDialogOptions> options)
    {
        _logger = logger;
        _audioDevice = audioDevice;
        _options = options.Value;
        
        // 创建音频数据通道
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        
        _inputAudioChannel = Channel.CreateBounded<AudioChunk>(channelOptions);
        _inputWriter = _inputAudioChannel.Writer;
        _inputReader = _inputAudioChannel.Reader;
        
        _outputAudioChannel = Channel.CreateBounded<AudioChunk>(channelOptions);
        _outputWriter = _outputAudioChannel.Writer;
        _outputReader = _outputAudioChannel.Reader;
        
        _audioSessions = new ConcurrentDictionary<string, AudioSession>();
        _deviceLock = new SemaphoreSlim(1, 1);
        
        // 定期更新统计信息
        _statisticsTimer = new Timer(UpdateStatistics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        
        // 订阅音频设备事件
        _audioDevice.AudioDataAvailable += OnAudioDataAvailable;
        _audioDevice.PlaybackCompleted += OnPlaybackCompleted;
        _audioDevice.DeviceError += OnDeviceError;
    }
    
    /// <summary>
    /// 音频数据可用事件
    /// </summary>
    public event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;
    
    /// <summary>
    /// 音频播放完成事件
    /// </summary>
    public event EventHandler<PlaybackCompletedEventArgs>? PlaybackCompleted;
    
    /// <summary>
    /// 音频处理错误事件
    /// </summary>
    public event EventHandler<AudioProcessingErrorEventArgs>? ProcessingError;
    
    /// <summary>
    /// 初始化音频处理服务
    /// </summary>
    /// <param name="config">音频配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化任务</returns>
    public async Task InitializeAsync(AudioConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            await _deviceLock.WaitAsync(cancellationToken);
            
            _logger.LogInformation("初始化音频处理服务...");
            
            // 初始化音频设备
            await _audioDevice.InitializeAsync(config, cancellationToken);
            
            // 启动音频处理任务
            _processingCancellation = new CancellationTokenSource();
            _inputProcessingTask = ProcessInputAudioAsync(_processingCancellation.Token);
            _outputProcessingTask = ProcessOutputAudioAsync(_processingCancellation.Token);
            
            _logger.LogInformation("音频处理服务初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化音频处理服务失败");
            throw;
        }
        finally
        {
            _deviceLock.Release();
        }
    }
    
    /// <summary>
    /// 创建音频会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="config">音频配置</param>
    /// <returns>音频会话</returns>
    public AudioSession CreateSession(string sessionId, AudioConfig config)
    {
        var session = new AudioSession
        {
            SessionId = sessionId,
            Config = config,
            Statistics = new AudioStatistics(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        _audioSessions.TryAdd(sessionId, session);
        _logger.LogDebug("创建音频会话: {SessionId}", sessionId);
        
        return session;
    }
    
    /// <summary>
    /// 获取音频会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>音频会话</returns>
    public AudioSession? GetSession(string sessionId)
    {
        _audioSessions.TryGetValue(sessionId, out var session);
        return session;
    }
    
    /// <summary>
    /// 移除音频会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveSession(string sessionId)
    {
        if (_audioSessions.TryRemove(sessionId, out var session))
        {
            session.IsActive = false;
            _logger.LogDebug("移除音频会话: {SessionId}", sessionId);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 开始录制音频
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>录制任务</returns>
    public async Task StartRecordingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"会话不存在: {sessionId}");
            }
            
            await _deviceLock.WaitAsync(cancellationToken);
            
            if (!_audioDevice.IsRecording)
            {
                await _audioDevice.StartRecordingAsync(cancellationToken);
            }
            
            session.IsRecording = true;
            session.RecordingStartedAt = DateTime.UtcNow;
            
            _logger.LogInformation("开始录制音频: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始录制音频失败: {SessionId}", sessionId);
            OnProcessingError(new AudioProcessingErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "RecordingStartFailed",
                ErrorMessage = ex.Message,
                Exception = ex
            });
            throw;
        }
        finally
        {
            _deviceLock.Release();
        }
    }
    
    /// <summary>
    /// 停止录制音频
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止任务</returns>
    public async Task StopRecordingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                return;
            }
            
            await _deviceLock.WaitAsync(cancellationToken);
            
            session.IsRecording = false;
            session.RecordingStoppedAt = DateTime.UtcNow;
            
            // 检查是否还有其他会话在录制
            var hasActiveRecording = _audioSessions.Values.Any(s => s.IsRecording);
            if (!hasActiveRecording && _audioDevice.IsRecording)
            {
                await _audioDevice.StopRecordingAsync(cancellationToken);
            }
            
            _logger.LogInformation("停止录制音频: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止录制音频失败: {SessionId}", sessionId);
            throw;
        }
        finally
        {
            _deviceLock.Release();
        }
    }
    
    /// <summary>
    /// 播放音频
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioChunk">音频块</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>播放任务</returns>
    public async Task PlayAudioAsync(string sessionId, AudioChunk audioChunk, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"会话不存在: {sessionId}");
            }
            
            // 将音频块添加到输出队列
            if (!_outputWriter.TryWrite(audioChunk))
            {
                _logger.LogWarning("输出音频队列已满，丢弃音频块: {SessionId}", sessionId);
                session.Statistics.DroppedChunks++;
            }
            else
            {
                session.Statistics.PlayedChunks++;
                _logger.LogDebug("添加音频块到播放队列: {SessionId}, 序列号: {SequenceNumber}", sessionId, audioChunk.SequenceNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "播放音频失败: {SessionId}", sessionId);
            OnProcessingError(new AudioProcessingErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "PlaybackFailed",
                ErrorMessage = ex.Message,
                Exception = ex
            });
            throw;
        }
    }
    
    /// <summary>
    /// 获取可用的输入设备
    /// </summary>
    /// <returns>设备列表</returns>
    public async Task<IEnumerable<AudioDeviceInfo>> GetInputDevicesAsync()
    {
        return await _audioDevice.GetInputDevicesAsync();
    }
    
    /// <summary>
    /// 获取可用的输出设备
    /// </summary>
    /// <returns>设备列表</returns>
    public async Task<IEnumerable<AudioDeviceInfo>> GetOutputDevicesAsync()
    {
        return await _audioDevice.GetOutputDevicesAsync();
    }
    
    /// <summary>
    /// 切换输入设备
    /// </summary>
    /// <param name="deviceId">设备ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切换任务</returns>
    public async Task SwitchInputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await _deviceLock.WaitAsync(cancellationToken);
        try
        {
            await _audioDevice.SwitchInputDeviceAsync(deviceId, cancellationToken);
            _logger.LogInformation("切换输入设备: {DeviceId}", deviceId);
        }
        finally
        {
            _deviceLock.Release();
        }
    }
    
    /// <summary>
    /// 切换输出设备
    /// </summary>
    /// <param name="deviceId">设备ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>切换任务</returns>
    public async Task SwitchOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await _deviceLock.WaitAsync(cancellationToken);
        try
        {
            await _audioDevice.SwitchOutputDeviceAsync(deviceId, cancellationToken);
            _logger.LogInformation("切换输出设备: {DeviceId}", deviceId);
        }
        finally
        {
            _deviceLock.Release();
        }
    }
    
    /// <summary>
    /// 获取音频统计信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>统计信息</returns>
    public AudioStatistics? GetStatistics(string sessionId)
    {
        return GetSession(sessionId)?.Statistics;
    }
    
    #region Private Methods
    
    private async Task ProcessInputAudioAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("开始处理输入音频流");
        
        try
        {
            await foreach (var audioChunk in _inputReader.ReadAllAsync(cancellationToken))
            {
                // 处理音频块
                await ProcessInputAudioChunk(audioChunk);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("输入音频处理已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理输入音频流时发生错误");
        }
    }
    
    private async Task ProcessOutputAudioAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("开始处理输出音频流");
        
        try
        {
            await foreach (var audioChunk in _outputReader.ReadAllAsync(cancellationToken))
            {
                // 播放音频块
                await _audioDevice.PlayAudioAsync(audioChunk, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("输出音频处理已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理输出音频流时发生错误");
        }
    }
    
    private async Task ProcessInputAudioChunk(AudioChunk audioChunk)
    {
        try
        {
            // 触发音频数据可用事件
            AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs
            {
                AudioChunk = audioChunk,
                RecordedAt = DateTime.UtcNow,
                AudioLevel = CalculateAudioLevel(audioChunk.Data),
                IsSilence = DetectSilence(audioChunk.Data)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理输入音频块时发生错误");
        }
    }
    
    private float CalculateAudioLevel(byte[] audioData)
    {
        if (audioData.Length == 0) return 0f;
        
        // 计算RMS音频级别
        long sum = 0;
        for (int i = 0; i < audioData.Length - 1; i += 2)
        {
            short sample = (short)(audioData[i] | (audioData[i + 1] << 8));
            sum += sample * sample;
        }
        
        double rms = Math.Sqrt((double)sum / (audioData.Length / 2));
        return (float)Math.Min(rms / 32768.0, 1.0);
    }
    
    private bool DetectSilence(byte[] audioData)
    {
        var level = CalculateAudioLevel(audioData);
        return level < _options.AudioConfig.SilenceThreshold;
    }
    
    private void OnAudioDataAvailable(object? sender, AudioDataAvailableEventArgs e)
    {
        // 将音频数据添加到输入队列
        if (!_inputWriter.TryWrite(e.AudioChunk))
        {
            _logger.LogWarning("输入音频队列已满，丢弃音频块");
        }
    }
    
    private void OnPlaybackCompleted(object? sender, PlaybackCompletedEventArgs e)
    {
        PlaybackCompleted?.Invoke(this, e);
    }
    
    private void OnDeviceError(object? sender, AudioDeviceErrorEventArgs e)
    {
        _logger.LogError("音频设备错误: {ErrorType} - {ErrorMessage}", e.ErrorType, e.ErrorMessage);
        
        OnProcessingError(new AudioProcessingErrorEventArgs
        {
            ErrorType = e.ErrorType.ToString(),
            ErrorMessage = e.ErrorMessage,
            Exception = e.Exception,
            IsRecoverable = e.IsRecoverable
        });
    }
    
    private void OnProcessingError(AudioProcessingErrorEventArgs e)
    {
        ProcessingError?.Invoke(this, e);
    }
    
    private void UpdateStatistics(object? state)
    {
        try
        {
            foreach (var session in _audioSessions.Values)
            {
                if (session.IsActive)
                {
                    session.Statistics.LastUpdated = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新音频统计信息时发生错误");
        }
    }
    
    #endregion
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogInformation("释放音频处理服务资源");
        
        _processingCancellation?.Cancel();
        
        _inputWriter.Complete();
        _outputWriter.Complete();
        
        try
        {
            Task.WaitAll(new[] { _inputProcessingTask, _outputProcessingTask }.Where(t => t != null).ToArray()!, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "等待音频处理任务完成时发生错误");
        }
        
        _statisticsTimer?.Dispose();
        _processingCancellation?.Dispose();
        _deviceLock?.Dispose();
        _audioDevice?.Dispose();
        
        _disposed = true;
    }
}

/// <summary>
/// 音频会话
/// </summary>
public class AudioSession
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频配置
    /// </summary>
    public AudioConfig Config { get; set; } = new();
    
    /// <summary>
    /// 统计信息
    /// </summary>
    public AudioStatistics Statistics { get; set; } = new();
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 是否活跃
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// 是否正在录制
    /// </summary>
    public bool IsRecording { get; set; }
    
    /// <summary>
    /// 录制开始时间
    /// </summary>
    public DateTime? RecordingStartedAt { get; set; }
    
    /// <summary>
    /// 录制停止时间
    /// </summary>
    public DateTime? RecordingStoppedAt { get; set; }
}

/// <summary>
/// 音频处理错误事件参数
/// </summary>
public class AudioProcessingErrorEventArgs : EventArgs
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;
    
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
    /// 错误时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 实时对话选项
/// </summary>
public class RealtimeDialogOptions
{
    /// <summary>
    /// 豆包API密钥
    /// </summary>
    public string DoubaoApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// WebSocket端点
    /// </summary>
    public string WebSocketEndpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频配置
    /// </summary>
    public AudioConfig AudioConfig { get; set; } = new();
    
    /// <summary>
    /// 最大并发会话数
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 100;
    
    /// <summary>
    /// 会话超时时间
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);
}