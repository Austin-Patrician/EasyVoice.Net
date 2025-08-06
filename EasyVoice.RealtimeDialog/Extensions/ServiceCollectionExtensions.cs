using EasyVoice.RealtimeDialog.Audio;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Models.Protocol;
using EasyVoice.RealtimeDialog.Models.Session;
using EasyVoice.RealtimeDialog.Protocols;
using EasyVoice.RealtimeDialog.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyVoice.RealtimeDialog.Extensions;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加实时对话服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRealtimeDialog(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置选项
        services.Configure<RealtimeDialogOptions>(configuration.GetSection("RealtimeDialog"));
        
        // 注册核心服务
        services.AddSingleton<DoubaoProtocolHandler>();
        services.AddSingleton<WebSocketClientManager>();
        services.AddSingleton<AudioProcessingService>();
        services.AddScoped<IRealtimeDialogService, RealtimeDialogService>();
        
        // 注册音频设备（需要根据平台选择具体实现）
        services.AddSingleton<IAudioDevice, DefaultAudioDevice>();
        
        return services;
    }
    
    /// <summary>
    /// 添加实时对话服务（使用委托配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRealtimeDialog(this IServiceCollection services, Action<RealtimeDialogOptions> configureOptions)
    {
        // 配置选项
        services.Configure(configureOptions);
        
        // 注册核心服务
        services.AddSingleton<DoubaoProtocolHandler>();
        services.AddSingleton<WebSocketClientManager>();
        services.AddSingleton<AudioProcessingService>();
        services.AddScoped<IRealtimeDialogService, RealtimeDialogService>();
        
        // 注册音频设备（需要根据平台选择具体实现）
        services.AddSingleton<IAudioDevice, DefaultAudioDevice>();
        
        return services;
    }
    
    /// <summary>
    /// 添加实时对话服务（使用自定义音频设备）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <param name="audioDeviceFactory">音频设备工厂</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRealtimeDialog<TAudioDevice>(this IServiceCollection services, IConfiguration configuration)
        where TAudioDevice : class, IAudioDevice
    {
        // 配置选项
        services.Configure<RealtimeDialogOptions>(configuration.GetSection("RealtimeDialog"));
        
        // 注册核心服务
        services.AddSingleton<DoubaoProtocolHandler>();
        services.AddSingleton<WebSocketClientManager>();
        services.AddSingleton<AudioProcessingService>();
        services.AddScoped<IRealtimeDialogService, RealtimeDialogService>();
        
        // 注册自定义音频设备
        services.AddSingleton<IAudioDevice, TAudioDevice>();
        
        return services;
    }
}

/// <summary>
/// 应用程序构建器扩展方法
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 使用实时对话服务
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>应用程序构建器</returns>
    public static IApplicationBuilder UseRealtimeDialog(this IApplicationBuilder app)
    {
        // 这里可以添加中间件或其他初始化逻辑
        return app;
    }
}

/// <summary>
/// 默认音频设备实现（占位符）
/// </summary>
internal class DefaultAudioDevice : IAudioDevice
{
    public AudioDeviceInfo DeviceInfo { get; } = new AudioDeviceInfo
    {
        DeviceId = "default",
        Name = "默认音频设备",
        Type = AudioDeviceType.Input | AudioDeviceType.Output,
        State = AudioDeviceState.Active,
        SupportedFormats = new[] { AudioFormat.CreateDefault16kHz() }
    };
    
    public bool IsRecording { get; private set; }
    public bool IsPlaying { get; private set; }
    public AudioStatistics Statistics { get; } = new();
    
    public event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;
    public event EventHandler<PlaybackCompletedEventArgs>? PlaybackCompleted;
    public event EventHandler<AudioDeviceErrorEventArgs>? DeviceError;
    public event EventHandler<DeviceStateChangedEventArgs>? DeviceStateChanged;
    
    public Task InitializeAsync(AudioConfig config, CancellationToken cancellationToken = default)
    {
        // 默认实现，实际使用时需要替换为具体的音频设备实现
        return Task.CompletedTask;
    }
    
    public Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        IsRecording = true;
        return Task.CompletedTask;
    }
    
    public Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        IsRecording = false;
        return Task.CompletedTask;
    }
    
    public Task PlayAudioAsync(AudioChunk audioChunk, CancellationToken cancellationToken = default)
    {
        IsPlaying = true;
        // 模拟播放完成
        Task.Delay(100, cancellationToken).ContinueWith(_ =>
        {
            IsPlaying = false;
            PlaybackCompleted?.Invoke(this, new PlaybackCompletedEventArgs
            {
                AudioChunk = audioChunk,
                CompletedAt = DateTime.UtcNow
            });
        }, cancellationToken);
        
        return Task.CompletedTask;
    }
    
    public Task StopPlaybackAsync(CancellationToken cancellationToken = default)
    {
        IsPlaying = false;
        return Task.CompletedTask;
    }
    
    public void SetInputVolume(float volume)
    {
        // 默认实现，不执行任何操作
    }
    
    public void SetOutputVolume(float volume)
    {
        // 默认实现，不执行任何操作
    }
    
    public Task<IEnumerable<AudioDeviceInfo>> GetInputDevicesAsync()
    {
        var devices = new[]
        {
            new AudioDeviceInfo
            {
                Id = "default-input",
                Name = "默认输入设备",
                Type = AudioDeviceType.Input,
                State = AudioDeviceState.Active,
                SupportedFormats = new[] { AudioFormat.CreateDefault16kHz() }
            }
        };
        
        return Task.FromResult<IEnumerable<AudioDeviceInfo>>(devices);
    }
    
    public Task<IEnumerable<AudioDeviceInfo>> GetOutputDevicesAsync()
    {
        var devices = new[]
        {
            new AudioDeviceInfo
            {
                Id = "default-output",
                Name = "默认输出设备",
                Type = AudioDeviceType.Output,
                State = AudioDeviceState.Active,
                SupportedFormats = new[] { AudioFormat.CreateDefault24kHz() }
            }
        };
        
        return Task.FromResult<IEnumerable<AudioDeviceInfo>>(devices);
    }
    
    public Task SwitchInputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    
    public Task SwitchOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        // 清理资源
    }
}