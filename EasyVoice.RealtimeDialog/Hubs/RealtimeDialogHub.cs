using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using EasyVoice.RealtimeDialog.Models;
using EasyVoice.RealtimeDialog.Models.Audio;

namespace EasyVoice.RealtimeDialog.Hubs;

public class RealtimeDialogHub : Hub
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<RealtimeDialogHub> _logger;
    private static readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public RealtimeDialogHub(IMemoryCache cache, ILogger<RealtimeDialogHub> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"客户端连接: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"客户端断开: {Context.ConnectionId}");
        // 清理该连接的所有会话
        await CleanupConnectionSessions();
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 创建对话会话
    /// </summary>
    public async Task<string> CreateSession(SessionConfig config)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                Config = config,
                ConnectionId = Context.ConnectionId
            };

            _sessions.TryAdd(sessionId, sessionInfo);
            _cache.Set($"session_{sessionId}", sessionInfo, TimeSpan.FromHours(1));

            await Clients.Caller.SendAsync("OnSessionStarted", sessionId);
            _logger.LogInformation($"会话创建成功: {sessionId}");
            
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            await Clients.Caller.SendAsync("OnError", $"创建会话失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 结束对话会话
    /// </summary>
    public async Task EndSession(string sessionId)
    {
        try
        {
            if (_sessions.TryRemove(sessionId, out var sessionInfo))
            {
                // 停止音频管理器
                sessionInfo.AudioManager?.Dispose();
                _cache.Remove($"session_{sessionId}");
                
                await Clients.Caller.SendAsync("OnSessionEnded", sessionId);
                _logger.LogInformation($"会话结束: {sessionId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"结束会话失败: {sessionId}");
            await Clients.Caller.SendAsync("OnError", $"结束会话失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置音频配置
    /// </summary>
    public async Task SetAudioConfig(AudioConfig config)
    {
        // 存储音频配置到连接上下文
        Context.Items["AudioConfig"] = config;
        await Task.CompletedTask;
    }

    /// <summary>
    /// 开始录制
    /// </summary>
    public async Task<bool> StartRecording()
    {
        try
        {
            await Clients.Caller.SendAsync("OnRecordingStarted", Context.ConnectionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始录制失败");
            await Clients.Caller.SendAsync("OnAudioError", $"开始录制失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 停止录制
    /// </summary>
    public async Task StopRecording()
    {
        try
        {
            await Clients.Caller.SendAsync("OnRecordingStopped", Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止录制失败");
            await Clients.Caller.SendAsync("OnAudioError", $"停止录制失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送音频数据
    /// </summary>
    public async Task SendAudio(string sessionId, byte[] audioData, bool isEnd)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var sessionInfo))
            {
                await Clients.Caller.SendAsync("OnError", "会话不存在");
                return;
            }

            // 初始化音频管理器（如果尚未初始化）
            if (sessionInfo.AudioManager == null)
            {
                await InitializeAudioManager(sessionInfo);
            }

            // 发送音频数据到豆包
            if (sessionInfo.AudioManager != null)
            {
                await sessionInfo.AudioManager.SendAudioAsync(audioData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"发送音频失败: {sessionId}");
            await Clients.Caller.SendAsync("OnError", $"发送音频失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送ChatTTS文本
    /// </summary>
    public async Task<bool> SendChatTtsText(string sessionId, string text)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var sessionInfo))
            {
                await Clients.Caller.SendAsync("OnError", "会话不存在");
                return false;
            }

            if (sessionInfo.AudioManager != null)
            {
                await sessionInfo.AudioManager.SendChatTtsTextAsync(text);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"发送ChatTTS文本失败: {sessionId}");
            await Clients.Caller.SendAsync("OnError", $"发送ChatTTS文本失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 初始化音频管理器
    /// </summary>
    private async Task InitializeAudioManager(SessionInfo sessionInfo)
    {
        try
        {
            // 构建WebSocket配置
            var wsConfig = new Dictionary<string, object>
            {
                ["base_url"] = "wss://openspeech.bytedance.com/api/v1/ws",
                ["headers"] = new Dictionary<string, string>
                {
                    ["X-Api-App-ID"] = sessionInfo.Config.AppId,
                    ["X-Api-Access-Key"] = sessionInfo.Config.AccessKey
                }
            };

            // 创建音频配置
            var inputConfig = new AudioConfigData
            {
                Format = sessionInfo.Config.AudioConfig.Format,
                BitSize = sessionInfo.Config.AudioConfig.BitDepth,
                Channels = sessionInfo.Config.AudioConfig.Channels,
                SampleRate = sessionInfo.Config.AudioConfig.SampleRate,
                Chunk = sessionInfo.Config.AudioConfig.ChunkSize
            };

            var outputConfig = new AudioConfigData
            {
                Format = "pcm",
                BitSize = 16,
                Channels = 1,
                SampleRate = 24000,
                Chunk = 4800
            };

            // 创建音频管理器
            sessionInfo.AudioManager = new DoubaoAudioManager(wsConfig, inputConfig, outputConfig);
            
            // 设置事件处理
            sessionInfo.AudioManager.OnAudioDataReceived += async (audioData) =>
            {
                await Clients.Client(sessionInfo.ConnectionId).SendAsync("OnTtsResponse", new { audioData });
            };

            sessionInfo.AudioManager.OnDialogEvent += async (eventType, eventData) =>
            {
                await HandleDialogEvent(sessionInfo.ConnectionId, eventType, eventData);
            };

            // 启动音频管理器
            _ = Task.Run(async () => await sessionInfo.AudioManager.StartAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化音频管理器失败");
            throw;
        }
    }

    /// <summary>
    /// 处理对话事件
    /// </summary>
    private async Task HandleDialogEvent(string connectionId, string eventType, object eventData)
    {
        switch (eventType)
        {
            case "ASR_INFO":
                await Clients.Client(connectionId).SendAsync("OnAsrInfo", eventData);
                break;
            case "ASR_RESPONSE":
                await Clients.Client(connectionId).SendAsync("OnAsrResponse", eventData);
                break;
            case "ASR_ENDED":
                await Clients.Client(connectionId).SendAsync("OnAsrEnded", eventData);
                break;
            default:
                _logger.LogWarning($"未知事件类型: {eventType}");
                break;
        }
    }

    /// <summary>
    /// 清理连接的所有会话
    /// </summary>
    private async Task CleanupConnectionSessions()
    {
        var sessionsToRemove = _sessions.Where(kvp => kvp.Value.ConnectionId == Context.ConnectionId).ToList();
        
        foreach (var session in sessionsToRemove)
        {
            if (_sessions.TryRemove(session.Key, out var sessionInfo))
            {
                sessionInfo.AudioManager?.Dispose();
                _cache.Remove($"session_{session.Key}");
            }
        }
        
        await Task.CompletedTask;
    }
}