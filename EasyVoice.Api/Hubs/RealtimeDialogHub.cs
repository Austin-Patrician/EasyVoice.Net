using EasyVoice.RealtimeDialog.Models;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EasyVoice.Api.Hubs;

/// <summary>
/// 实时语音对话SignalR Hub
/// 提供实时双向通信功能
/// </summary>
public class RealtimeDialogHub : Hub
{
    private readonly RealtimeDialogService _dialogService;
    private readonly AudioService _audioService;
    private readonly ILogger<RealtimeDialogHub> _logger;
    
    // 连接ID到会话ID的映射
    private static readonly ConcurrentDictionary<string, string> _connectionSessions = new();
    
    public RealtimeDialogHub(
        RealtimeDialogService dialogService,
        AudioService audioService,
        ILogger<RealtimeDialogHub> logger)
    {
        _dialogService = dialogService;
        _audioService = audioService;
        _logger = logger;
        
        // 订阅服务事件
        SubscribeToServiceEvents();
    }
    
    /// <summary>
    /// 订阅服务事件
    /// </summary>
    private void SubscribeToServiceEvents()
    {
        // 订阅对话服务事件
        _dialogService.OnAsrInfo += async (sender, e) => 
        {
            await Clients.All.SendAsync("OnAsrInfo", e);
        };
        
        _dialogService.OnAsrResponse += async (sender, e) => 
        {
            await Clients.All.SendAsync("OnAsrResponse", e);
        };
        
        _dialogService.OnAsrEnded += async (sender, e) => 
        {
            await Clients.All.SendAsync("OnAsrEnded", e);
        };
        
        _dialogService.OnTtsResponse += async (sender, e) => 
        {
            await Clients.All.SendAsync("OnTtsResponse", e);
        };
        
        _dialogService.OnSessionStarted += async (sender, sessionId) => 
        {
            await Clients.All.SendAsync("OnSessionStarted", sessionId);
        };
        
        _dialogService.OnSessionEnded += async (sessionId) => 
        {
            await Clients.All.SendAsync("OnSessionEnded", sessionId);
        };
        
        _dialogService.OnError += async (error) => 
        {
            await Clients.All.SendAsync("OnError", error);
        };
        
        // 订阅音频服务事件
        _audioService.AudioDataReceived += async (sender, packet) => 
        {
            await Clients.All.SendAsync("OnAudioDataReceived", packet);
        };
        
        _audioService.RecordingStarted += async (sender, deviceId) => 
        {
            await Clients.All.SendAsync("OnRecordingStarted", deviceId);
        };
        
        _audioService.RecordingStopped += async (sender, deviceId) => 
        {
            await Clients.All.SendAsync("OnRecordingStopped", deviceId);
        };
        
        _audioService.PlaybackStarted += async (sender, deviceId) => 
        {
            await Clients.All.SendAsync("OnPlaybackStarted", deviceId);
        };
        
        _audioService.PlaybackStopped += async (sender, deviceId) => 
        {
            await Clients.All.SendAsync("OnPlaybackStopped", deviceId);
        };
        
        _audioService.AudioError += async (sender, ex) => 
        {
            await Clients.All.SendAsync("OnAudioError", ex.Message);
        };
    }
    
    /// <summary>
    /// 客户端连接时调用
    /// </summary>
    /// <returns></returns>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("客户端连接: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// 客户端断开连接时调用
    /// </summary>
    /// <param name="exception">异常信息</param>
    /// <returns></returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("客户端断开连接: {ConnectionId}, 异常: {Exception}", 
            Context.ConnectionId, exception?.Message);
        
        // 清理会话映射
        if (_connectionSessions.TryRemove(Context.ConnectionId, out var sessionId))
        {
            _logger.LogInformation("清理会话映射: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
            // 可以选择自动结束会话
            // await _dialogService.EndSessionAsync(sessionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// 创建新的对话会话
    /// </summary>
    /// <param name="config">会话配置</param>
    /// <returns></returns>
    public async Task<string> CreateSession(SessionConfig config)
    {
        try
        {
            var sessionId = await _dialogService.StartSessionAsync(config);
            if (!string.IsNullOrEmpty(sessionId))
            {
                _connectionSessions.TryAdd(Context.ConnectionId, sessionId);
                _logger.LogInformation("会话创建成功: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
            }
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task<bool> EndSession(string sessionId)
    {
        try
        {
            var success = await _dialogService.EndSessionAsync(sessionId);
            if (success)
            {
                _connectionSessions.TryRemove(Context.ConnectionId, out _);
                _logger.LogInformation("会话结束成功: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话失败: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
            throw;
        }
    }
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioData">音频数据</param>
    /// <param name="isLast">是否为最后一个包</param>
    /// <returns></returns>
    public async Task<bool> SendAudio(string sessionId, byte[] audioData, bool isLast = false)
    {
        try
        {
            return await _dialogService.SendAudioAsync(sessionId, audioData, isLast);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频失败: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
            throw;
        }
    }
    
    /// <summary>
    /// 发送ChatTTSText请求
    /// </summary>
    /// <param name="text">要合成的文本</param>
    /// <param name="voiceId">语音ID</param>
    /// <param name="speed">语速</param>
    /// <param name="emotion">情感</param>
    /// <returns></returns>
    public async Task<bool> SendChatTtsText(string text, string? voiceId = null, float speed = 1.0f, string? emotion = null)
    {
        try
        {
            return await _dialogService.SendChatTtsTextAsync(text, voiceId, speed, emotion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送ChatTTSText失败: {ConnectionId}, Text: {Text}", Context.ConnectionId, text);
            throw;
        }
    }
    
    /// <summary>
    /// 获取会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task<DialogSession?> GetSessionInfo(string sessionId)
    {
        try
        {
            return await _dialogService.GetSessionInfoAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话信息失败: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
            throw;
        }
    }
    
    /// <summary>
    /// 开始录制音频
    /// </summary>
    /// <param name="deviceId">设备ID</param>
    /// <returns></returns>
    public async Task<bool> StartRecording(string? deviceId = null)
    {
        try
        {
            return await _audioService.StartRecordingAsync(deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始录制失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 停止录制音频
    /// </summary>
    /// <returns></returns>
    public async Task<bool> StopRecording()
    {
        try
        {
            return await _audioService.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止录制失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 播放音频
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <param name="deviceId">设备ID</param>
    /// <returns></returns>
    public async Task<bool> PlayAudio(byte[] audioData, string? deviceId = null)
    {
        try
        {
            return await _audioService.PlayAudioAsync(audioData, deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "播放音频失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 获取音频设备列表
    /// </summary>
    /// <returns></returns>
    public object GetAudioDevices()
    {
        try
        {
            return new
            {
                inputDevices = _audioService.GetInputDevices(),
                outputDevices = _audioService.GetOutputDevices(),
                defaultInput = _audioService.GetDefaultInputDevice(),
                defaultOutput = _audioService.GetDefaultOutputDevice()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取音频设备失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 设置音频配置
    /// </summary>
    /// <param name="config">音频配置</param>
    /// <returns></returns>
    public bool SetAudioConfig(AudioConfig config)
    {
        try
        {
            return _audioService.SetAudioConfig(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置音频配置失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 获取录制状态
    /// </summary>
    /// <returns></returns>
    public object GetRecordingStatus()
    {
        try
        {
            return new
            {
                isRecording = _audioService.IsRecording,
                isPlaying = _audioService.IsPlaying,
                currentConfig = _audioService.GetCurrentConfig()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取录制状态失败: {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }
    
    /// <summary>
    /// 加入会话组
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");
        _connectionSessions.TryAdd(Context.ConnectionId, sessionId);
        _logger.LogInformation("客户端加入会话组: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
    }
    
    /// <summary>
    /// 离开会话组
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");
        _connectionSessions.TryRemove(Context.ConnectionId, out _);
        _logger.LogInformation("客户端离开会话组: {ConnectionId} -> {SessionId}", Context.ConnectionId, sessionId);
    }
}