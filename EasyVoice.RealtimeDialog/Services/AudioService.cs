using EasyVoice.RealtimeDialog.Models.Audio;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// 音频处理服务
/// 负责音频录制、播放、格式转换等功能
/// </summary>
public class AudioService
{
    private readonly ILogger<AudioService> _logger;
    private readonly ConcurrentDictionary<string, AudioDeviceInfo> _inputDevices = new();
    private readonly ConcurrentDictionary<string, AudioDeviceInfo> _outputDevices = new();
    private bool _isRecording;
    private bool _isPlaying;
    private AudioConfig _currentConfig = new();
    private readonly object _lockObject = new();
    
    // 事件定义
    public event EventHandler<AudioPacket>? AudioDataReceived;
    public event EventHandler<string>? RecordingStarted;
    public event EventHandler<string>? RecordingStopped;
    public event EventHandler<string>? PlaybackStarted;
    public event EventHandler<string>? PlaybackStopped;
    public event EventHandler<Exception>? AudioError;
    
    public AudioService(ILogger<AudioService> logger)
    {
        _logger = logger;
        InitializeAudioDevices();
    }
    
    /// <summary>
    /// 初始化音频设备
    /// </summary>
    private void InitializeAudioDevices()
    {
        try
        {
            // 这里应该调用系统API获取音频设备列表
            // 为了演示，添加一些默认设备
            var defaultInput = new AudioDeviceInfo
            {
                DeviceId = "default_input",
                DeviceName = "Default Input Device",
                IsInput = true,
                IsDefault = true,
                SupportedFormats = new List<AudioConfig>
                {
                    new() { SampleRate = 16000, Channels = 1, Format = "pcm", BitDepth = 16 },
                    new() { SampleRate = 24000, Channels = 1, Format = "pcm", BitDepth = 16 },
                    new() { SampleRate = 48000, Channels = 2, Format = "pcm", BitDepth = 16 }
                }
            };
            
            var defaultOutput = new AudioDeviceInfo
            {
                DeviceId = "default_output",
                DeviceName = "Default Output Device",
                IsOutput = true,
                IsDefault = true,
                SupportedFormats = new List<AudioConfig>
                {
                    new() { SampleRate = 16000, Channels = 1, Format = "pcm", BitDepth = 16 },
                    new() { SampleRate = 24000, Channels = 1, Format = "pcm", BitDepth = 16 },
                    new() { SampleRate = 48000, Channels = 2, Format = "pcm", BitDepth = 16 }
                }
            };
            
            _inputDevices.TryAdd(defaultInput.DeviceId, defaultInput);
            _outputDevices.TryAdd(defaultOutput.DeviceId, defaultOutput);
            
            _logger.LogInformation("音频设备初始化完成，输入设备: {InputCount}, 输出设备: {OutputCount}", 
                _inputDevices.Count, _outputDevices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化音频设备失败");
            AudioError?.Invoke(this, ex);
        }
    }
    
    /// <summary>
    /// 获取所有输入设备
    /// </summary>
    /// <returns></returns>
    public List<AudioDeviceInfo> GetInputDevices()
    {
        return _inputDevices.Values.ToList();
    }
    
    /// <summary>
    /// 获取所有输出设备
    /// </summary>
    /// <returns></returns>
    public List<AudioDeviceInfo> GetOutputDevices()
    {
        return _outputDevices.Values.ToList();
    }
    
    /// <summary>
    /// 获取默认输入设备
    /// </summary>
    /// <returns></returns>
    public AudioDeviceInfo? GetDefaultInputDevice()
    {
        return _inputDevices.Values.FirstOrDefault(d => d.IsDefault);
    }
    
    /// <summary>
    /// 获取默认输出设备
    /// </summary>
    /// <returns></returns>
    public AudioDeviceInfo? GetDefaultOutputDevice()
    {
        return _outputDevices.Values.FirstOrDefault(d => d.IsDefault);
    }
    
    /// <summary>
    /// 设置音频配置
    /// </summary>
    /// <param name="config">音频配置</param>
    /// <returns></returns>
    public bool SetAudioConfig(AudioConfig config)
    {
        lock (_lockObject)
        {
            if (_isRecording || _isPlaying)
            {
                _logger.LogWarning("无法在录制或播放期间更改音频配置");
                return false;
            }
            
            _currentConfig = config;
            _logger.LogInformation("音频配置已更新: {SampleRate}Hz, {Channels}声道, {Format}, {BitDepth}位", 
                config.SampleRate, config.Channels, config.Format, config.BitDepth);
            return true;
        }
    }
    
    /// <summary>
    /// 开始录制音频
    /// </summary>
    /// <param name="deviceId">设备ID，null表示使用默认设备</param>
    /// <returns></returns>
    public async Task<bool> StartRecordingAsync(string? deviceId = null)
    {
        lock (_lockObject)
        {
            if (_isRecording)
            {
                _logger.LogWarning("录制已在进行中");
                return false;
            }
            
            _isRecording = true;
        }
        
        try
        {
            var device = deviceId != null && _inputDevices.TryGetValue(deviceId, out var d) 
                ? d : GetDefaultInputDevice();
                
            if (device == null)
            {
                _logger.LogError("未找到可用的输入设备");
                return false;
            }
            
            _logger.LogInformation("开始录制音频，设备: {DeviceName}", device.DeviceName);
            
            // 这里应该启动实际的音频录制
            // 为了演示，我们启动一个模拟录制的任务
            _ = Task.Run(() => SimulateAudioRecording(device));
            
            RecordingStarted?.Invoke(this, device.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            lock (_lockObject)
            {
                _isRecording = false;
            }
            _logger.LogError(ex, "开始录制音频失败");
            AudioError?.Invoke(this, ex);
            return false;
        }
    }
    
    /// <summary>
    /// 停止录制音频
    /// </summary>
    /// <returns></returns>
    public async Task<bool> StopRecordingAsync()
    {
        lock (_lockObject)
        {
            if (!_isRecording)
            {
                _logger.LogWarning("当前没有进行录制");
                return false;
            }
            
            _isRecording = false;
        }
        
        try
        {
            _logger.LogInformation("停止录制音频");
            RecordingStopped?.Invoke(this, "recording_stopped");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止录制音频失败");
            AudioError?.Invoke(this, ex);
            return false;
        }
    }
    
    /// <summary>
    /// 播放音频数据
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <param name="deviceId">设备ID，null表示使用默认设备</param>
    /// <returns></returns>
    public async Task<bool> PlayAudioAsync(byte[] audioData, string? deviceId = null)
    {
        if (audioData == null || audioData.Length == 0)
        {
            _logger.LogWarning("音频数据为空");
            return false;
        }
        
        try
        {
            var device = deviceId != null && _outputDevices.TryGetValue(deviceId, out var d) 
                ? d : GetDefaultOutputDevice();
                
            if (device == null)
            {
                _logger.LogError("未找到可用的输出设备");
                return false;
            }
            
            _logger.LogInformation("播放音频数据，大小: {Size} 字节，设备: {DeviceName}", 
                audioData.Length, device.DeviceName);
            
            // 这里应该调用实际的音频播放API
            // 为了演示，我们模拟播放过程
            PlaybackStarted?.Invoke(this, device.DeviceId);
            
            // 模拟播放延迟
            await Task.Delay(100);
            
            PlaybackStopped?.Invoke(this, device.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "播放音频失败");
            AudioError?.Invoke(this, ex);
            return false;
        }
    }
    
    /// <summary>
    /// 模拟音频录制
    /// </summary>
    /// <param name="device">音频设备</param>
    private async Task SimulateAudioRecording(AudioDeviceInfo device)
    {
        uint sequenceNumber = 0;
        
        while (_isRecording)
        {
            try
            {
                // 模拟生成音频数据
                var audioData = new byte[_currentConfig.ChunkSize];
                // 这里应该从实际音频设备读取数据
                
                var packet = new AudioPacket
                {
                    Data = audioData,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    SequenceNumber = sequenceNumber++,
                    IsLast = false,
                    Format = _currentConfig
                };
                
                AudioDataReceived?.Invoke(this, packet);
                
                // 根据采样率计算延迟
                var delayMs = (_currentConfig.ChunkSize * 1000) / (_currentConfig.SampleRate * _currentConfig.Channels * (_currentConfig.BitDepth / 8));
                await Task.Delay(Math.Max(10, delayMs));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "录制音频数据时发生错误");
                AudioError?.Invoke(this, ex);
                break;
            }
        }
    }
    
    /// <summary>
    /// 转换音频格式
    /// </summary>
    /// <param name="audioData">原始音频数据</param>
    /// <param name="sourceConfig">源格式配置</param>
    /// <param name="targetConfig">目标格式配置</param>
    /// <returns></returns>
    public byte[] ConvertAudioFormat(byte[] audioData, AudioConfig sourceConfig, AudioConfig targetConfig)
    {
        try
        {
            // 这里应该实现实际的音频格式转换
            // 为了演示，我们直接返回原数据
            _logger.LogInformation("转换音频格式: {SourceFormat} -> {TargetFormat}", 
                sourceConfig.Format, targetConfig.Format);
            
            return audioData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换音频格式失败");
            AudioError?.Invoke(this, ex);
            return audioData;
        }
    }
    
    /// <summary>
    /// 获取当前录制状态
    /// </summary>
    /// <returns></returns>
    public bool IsRecording => _isRecording;
    
    /// <summary>
    /// 获取当前播放状态
    /// </summary>
    /// <returns></returns>
    public bool IsPlaying => _isPlaying;
    
    /// <summary>
    /// 获取当前音频配置
    /// </summary>
    /// <returns></returns>
    public AudioConfig GetCurrentConfig() => _currentConfig;
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public async Task DisposeAsync()
    {
        try
        {
            if (_isRecording)
            {
                await StopRecordingAsync();
            }
            
            _inputDevices.Clear();
            _outputDevices.Clear();
            
            _logger.LogInformation("音频服务已释放");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放音频服务时发生错误");
        }
    }
}