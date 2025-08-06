using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using EasyVoice.RealtimeDialog.Models;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Models.Protocol;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// 实时对话服务
/// 实现完整的server_vad模式交互流程
/// </summary>
public class RealtimeDialogService : IDisposable
{
    private readonly ILogger<RealtimeDialogService> _logger;
    private readonly WebSocketClientManager _webSocketManager;
    private readonly ConcurrentDictionary<string, DialogSession> _sessions;
    private bool _disposed;
    
    // 事件定义
    public event Func<string, DoubaoResponse, Task>? OnSessionEvent;
    public event Func<string, byte[], Task>? OnAudioReceived;
    public event Func<string, string, Task>? OnTextReceived;
    public event Func<string, Exception, Task>? OnSessionError;
    public event Func<string, Task>? OnSessionEnded;
    public event Func<Exception, Task>? OnError;
    
    // 新增的事件定义
    public event EventHandler<DoubaoResponse>? OnAsrInfo;
    public event EventHandler<DoubaoResponse>? OnAsrResponse;
    public event EventHandler<DoubaoResponse>? OnAsrEnded;
    public event EventHandler<DoubaoResponse>? OnTtsResponse;
    public event EventHandler<string>? OnSessionStarted;
    
    public RealtimeDialogService(
        ILogger<RealtimeDialogService> logger,
        WebSocketClientManager webSocketManager)
    {
        _logger = logger;
        _webSocketManager = webSocketManager;
        _sessions = new ConcurrentDictionary<string, DialogSession>();
        
        // 订阅WebSocket事件
        _webSocketManager.OnMessageReceived += HandleWebSocketMessage;
        _webSocketManager.OnError += HandleWebSocketError;
        _webSocketManager.OnDisconnected += HandleWebSocketDisconnected;
    }
    
    /// <summary>
    /// 连接到豆包实时语音API
    /// </summary>
    public async Task<bool> ConnectAsync(string appId, string accessKey, string? connectId = null)
    {
        return await _webSocketManager.ConnectAsync(appId, accessKey, connectId);
    }
    
    /// <summary>
    /// 开始新的对话会话
    /// </summary>
    public async Task<string> StartSessionAsync(SessionConfig config)
    {
        var sessionId = Guid.NewGuid().ToString();
        
        var session = new DialogSession
        {
            SessionId = sessionId,
            Config = config,
            Status = SessionStatus.Starting,
            CreatedAt = DateTimeOffset.UtcNow,
            IsUserQuerying = false,
            IsSendingChatTtsText = false
        };
        
        _sessions[sessionId] = session;
        
        // 构建StartSession请求配置
        var sessionRequestConfig = new
        {
            tts = new
            {
                audio_config = new
                {
                    channel = config.AudioConfig.Channels,
                    format = config.AudioConfig.Format,
                    sample_rate = config.AudioConfig.SampleRate
                },
                speaker = config.Speaker ?? "zh_female_vv_jupiter_bigtts"
            },
            dialog = new
            {
                bot_name = config.BotName ?? "豆包",
                system_role = config.SystemRole ?? "你使用活泼灵动的女声，性格开朗，热爱生活。",
                speaking_style = config.SpeakingStyle ?? "你的说话风格简洁明了，语速适中，语调自然。",
                extra = new
                {
                    strict_audit = false,
                    audit_response = "支持客户自定义安全审核回复话术。"
                }
            }
        };
        
        var success = await _webSocketManager.StartSessionAsync(sessionId, sessionRequestConfig);
        if (success)
        {
            session.Status = SessionStatus.Active;
            _logger.LogInformation("会话已开始: {SessionId}", sessionId);
            
            // 触发SessionStarted事件
            OnSessionStarted?.Invoke(this, sessionId);
        }
        else
        {
            session.Status = SessionStatus.Failed;
            _sessions.TryRemove(sessionId, out _);
            throw new InvalidOperationException("开始会话失败");
        }
        
        return sessionId;
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    public async Task<bool> EndSessionAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("会话不存在: {SessionId}", sessionId);
            return false;
        }
        
        session.Status = SessionStatus.Ending;
        
        var success = await _webSocketManager.FinishSessionAsync(sessionId);
        if (success)
        {
            session.Status = SessionStatus.Ended;
            _sessions.TryRemove(sessionId, out _);
            _logger.LogInformation("会话已结束: {SessionId}", sessionId);
        }
        else
        {
            session.Status = SessionStatus.Failed;
            _logger.LogError("结束会话失败: {SessionId}", sessionId);
        }
        
        return success;
    }
    
    /// <summary>
    /// 发送Hello消息
    /// </summary>
    public async Task<bool> SayHelloAsync()
    {
        return await _webSocketManager.SayHelloAsync();
    }
    
    /// <summary>
    /// 获取会话信息
    /// </summary>
    public DialogSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }
    
    /// <summary>
    /// 获取所有活跃会话
    /// </summary>
    public IEnumerable<DialogSession> GetActiveSessions()
    {
        return _sessions.Values.Where(s => s.Status == SessionStatus.Active);
    }
    
    /// <summary>
    /// 发送ChatTTSText请求
    /// </summary>
    public async Task<bool> SendChatTtsTextAsync(string text, string? voiceId = null, float speed = 1.0f, string? emotion = null)
    {
        // 获取第一个活跃会话，或者可以根据需要修改为接受sessionId参数
        var activeSession = GetActiveSessions().FirstOrDefault();
        if (activeSession == null)
        {
            _logger.LogWarning("没有活跃的会话可以发送ChatTTSText");
            return false;
        }
        
        return await _webSocketManager.SendChatTtsTextAsync(activeSession.SessionId, text, true, true);
    }
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    public async Task<bool> SendAudioAsync(byte[] audioData)
    {
        // 获取第一个活跃会话，或者可以根据需要修改为接受sessionId参数
        var activeSession = GetActiveSessions().FirstOrDefault();
        if (activeSession == null)
        {
            _logger.LogWarning("没有活跃的会话可以发送音频");
            return false;
        }
        
        return await _webSocketManager.SendAudioAsync(activeSession.SessionId, audioData);
    }
    
    /// <summary>
    /// 发送音频数据（带会话ID和结束标志）
    /// </summary>
    public async Task<bool> SendAudioAsync(string sessionId, byte[] audioData, bool isLast = false)
    {
        if (!_sessions.ContainsKey(sessionId))
        {
            _logger.LogWarning("会话不存在: {SessionId}", sessionId);
            return false;
        }
        
        return await _webSocketManager.SendAudioAsync(sessionId, audioData);
    }
    
    /// <summary>
    /// 获取会话信息（异步版本）
    /// </summary>
    public async Task<DialogSession?> GetSessionInfoAsync(string sessionId)
    {
        await Task.CompletedTask; // 保持异步签名
        return GetSession(sessionId);
    }
    
    /// <summary>
    /// 处理WebSocket消息
    /// </summary>
    private async Task HandleWebSocketMessage(DoubaoResponse response)
    {
        try
        {
            _logger.LogDebug("收到WebSocket消息: Event={Event}, SessionId={SessionId}", 
                response.Event, response.SessionId);
            
            if (response?.Event != null)
            {
                await HandleServerResponse(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理WebSocket消息失败: Event={Event}", response?.Event);
        }
    }
    
    /// <summary>
    /// 处理服务器响应
    /// </summary>
    private async Task HandleServerResponse(DoubaoResponse response)
    {
        var eventType = response.Header?.Event;
        var sessionId = response.Header?.SessionId ?? string.Empty;
        
        switch (eventType)
        {
            case 450: // ASRInfo - 清空音频缓存，设置用户查询状态
                await HandleServerFullResponse(response);
                break;
                
            case 451: // ASRResponse - 处理ASR识别结果
                await HandleServerAck(response);
                
                // 触发ASRResponse事件
                OnAsrResponse?.Invoke(this, response);
                break;
                
            case 459: // ASREnded - 结束ASR，触发ChatTTSText
                await HandleServerFullResponse(response);
                
                // 触发ASREnded事件
                OnAsrEnded?.Invoke(this, response);
                break;
                
            case 350: // TTSResponse - 处理TTS音频数据
                await HandleServerFullResponse(response);
                
                // 触发TTSResponse事件
                OnTtsResponse?.Invoke(this, response);
                break;
                
            case 152: // SessionFinished
            case 153: // SessionFinished
                await HandleServerFullResponse(response);
                break;
                
            default:
                await HandleServerAck(response);
                break;
        }
        
        // 触发事件
        if (OnSessionEvent != null)
        {
            await OnSessionEvent(sessionId, response);
        }
    }
    
    /// <summary>
    /// 处理服务器完整响应
    /// </summary>
    private async Task HandleServerFullResponse(DoubaoResponse response)
    {
        var eventType = response.Header?.Event;
        var sessionId = response.Header?.SessionId ?? string.Empty;
        
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }
        
        switch (eventType)
        {
            case 450: // ASRInfo - 清空音频缓存，设置用户查询状态
                session.AudioBuffer.Clear();
                session.IsUserQuerying = true;
                _logger.LogDebug("ASRInfo事件: 清空音频缓存，设置用户查询状态 - {SessionId}", sessionId);
                
                // 触发ASRInfo事件
                OnAsrInfo?.Invoke(this, response);
                break;
                
            case 459: // ASREnded - 结束ASR，触发ChatTTSText
                session.IsUserQuerying = false;
                _logger.LogDebug("ASREnded事件: 结束ASR - {SessionId}", sessionId);
                
                // 触发ChatTTSText
                await TriggerChatTtsTextAsync(sessionId);
                break;
                
            case 350: // TTSResponse - 处理TTS音频数据
                if (response.Payload != null)
                {
                    // 尝试从Payload中提取音频数据
                    if (response.PayloadMsg is byte[] audioBytes)
                    {
                        if (OnAudioReceived != null)
                        {
                            await OnAudioReceived(sessionId, audioBytes);
                        }
                    }
                    else if (response.PayloadMsg is string audioBase64)
                    {
                        try
                        {
                            var audioData = Convert.FromBase64String(audioBase64);
                            if (OnAudioReceived != null)
                            {
                                await OnAudioReceived(sessionId, audioData);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "解析音频数据失败: {SessionId}", sessionId);
                        }
                    }
                }
                break;
                
            case 152: // SessionFinished
            case 153: // SessionFinished
                session.Status = SessionStatus.Ended;
                _sessions.TryRemove(sessionId, out _);
                _logger.LogInformation("会话结束: {SessionId}", sessionId);
                
                // 触发会话结束事件
                if (OnSessionEnded != null)
                {
                    await OnSessionEnded(sessionId);
                }
                break;
        }
        
        session.LastActivityTime = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// 处理服务器确认响应
    /// </summary>
    private async Task HandleServerAck(DoubaoResponse response)
    {
        var sessionId = response.Header?.SessionId ?? string.Empty;
        var eventType = response.Header?.Event;
        
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }
        
        if (eventType == 451 && response.Payload != null) // ASRResponse
        {
            // 尝试从Payload中提取文本数据
            string? text = null;
            if (response.PayloadMsg is string textString)
            {
                text = textString;
            }
            else if (response.PayloadMsg is JsonElement jsonElement && jsonElement.TryGetProperty("text", out var textProperty))
            {
                text = textProperty.GetString();
            }
            
            if (!string.IsNullOrEmpty(text) && OnTextReceived != null)
            {
                await OnTextReceived(sessionId, text);
            }
        }
        
        session.LastActivityTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("服务器确认响应: Event={Event}, SessionId={SessionId}", eventType, sessionId);
    }
    
    /// <summary>
    /// 处理服务器错误
    /// </summary>
    private async Task HandleServerError(DoubaoResponse response)
    {
        var sessionId = response.Header?.SessionId ?? string.Empty;
        
        // 尝试从Payload中提取错误消息
        string errorMessage = "未知错误";
        if (response.PayloadMsg is string textString)
        {
            errorMessage = textString;
        }
        else if (response.PayloadMsg is JsonElement jsonElement && jsonElement.TryGetProperty("message", out var messageProperty))
        {
            errorMessage = messageProperty.GetString() ?? "未知错误";
        }
        
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Status = SessionStatus.Failed;
            session.ErrorMessage = errorMessage;
        }
        
        _logger.LogError("服务器错误: {ErrorMessage}, SessionId: {SessionId}", errorMessage, sessionId);
        
        if (OnSessionError != null)
        {
            await OnSessionError(sessionId, new Exception(errorMessage));
        }
    }
    
    /// <summary>
    /// 处理WebSocket错误
    /// </summary>
    private async Task HandleWebSocketError(Exception exception)
    {
        _logger.LogError(exception, "WebSocket错误");
        
        // 触发全局错误事件
        if (OnError != null)
        {
            await OnError(exception);
        }
        
        // 将所有活跃会话标记为断开连接
        foreach (var session in _sessions.Values.Where(s => s.Status == SessionStatus.Active))
        {
            session.Status = SessionStatus.Disconnected;
            session.ErrorMessage = exception.Message;
            
            if (OnSessionError != null)
            {
                await OnSessionError(session.SessionId, exception);
            }
        }
    }
    
    /// <summary>
    /// 处理WebSocket断开连接
    /// </summary>
    private async Task HandleWebSocketDisconnected()
    {
        _logger.LogWarning("WebSocket连接已断开");
        
        // 将所有活跃会话标记为断开连接
        foreach (var session in _sessions.Values.Where(s => s.Status == SessionStatus.Active))
        {
            session.Status = SessionStatus.Disconnected;
            
            if (OnSessionError != null)
            {
                await OnSessionError(session.SessionId, new Exception("WebSocket连接已断开"));
            }
        }
    }
    
    /// <summary>
    /// 触发ChatTTSText
    /// </summary>
    private async Task TriggerChatTtsTextAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }
        
        if (session.IsSendingChatTtsText)
        {
            _logger.LogDebug("正在发送ChatTTSText，跳过触发 - {SessionId}", sessionId);
            return;
        }
        
        // 这里可以根据业务逻辑生成回复内容
        var replyContent = "我收到了您的消息。";
        
        await _webSocketManager.SendChatTtsTextAsync(sessionId, replyContent, true, true);
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        // 取消订阅事件
        _webSocketManager.OnMessageReceived -= HandleWebSocketMessage;
        _webSocketManager.OnError -= HandleWebSocketError;
        _webSocketManager.OnDisconnected -= HandleWebSocketDisconnected;
        
        // 清理所有会话
        _sessions.Clear();
        
        _disposed = true;
        _logger.LogInformation("RealtimeDialogService已释放");
    }
}