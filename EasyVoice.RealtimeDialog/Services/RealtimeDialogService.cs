using System.Collections.Concurrent;
using EasyVoice.RealtimeDialog.Audio;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Models.Protocol;
using EasyVoice.RealtimeDialog.Models.Session;
using EasyVoice.RealtimeDialog.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// 实时对话服务实现
/// </summary>
public class RealtimeDialogService : IRealtimeDialogService, IDisposable
{
    private readonly ILogger<RealtimeDialogService> _logger;
    private readonly WebSocketClientManager _webSocketManager;
    private readonly AudioProcessingService _audioService;
    private readonly DoubaoProtocolHandler _protocolHandler;
    private readonly RealtimeDialogOptions _options;
    
    private readonly ConcurrentDictionary<string, DialogSession> _sessions;
    private readonly SemaphoreSlim _sessionLock;
    private readonly Timer _sessionCleanupTimer;
    
    private bool _disposed;
    
    public RealtimeDialogService(
        ILogger<RealtimeDialogService> logger,
        WebSocketClientManager webSocketManager,
        AudioProcessingService audioService,
        DoubaoProtocolHandler protocolHandler,
        IOptions<RealtimeDialogOptions> options)
    {
        _logger = logger;
        _webSocketManager = webSocketManager;
        _audioService = audioService;
        _protocolHandler = protocolHandler;
        _options = options.Value;
        
        _sessions = new ConcurrentDictionary<string, DialogSession>();
        _sessionLock = new SemaphoreSlim(1, 1);
        
        // 定期清理过期会话
        _sessionCleanupTimer = new Timer(CleanupExpiredSessions, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        
        // 订阅事件
        _webSocketManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        _webSocketManager.MessageReceived += OnMessageReceived;
        _webSocketManager.ConnectionError += OnConnectionError;
        
        _audioService.AudioDataAvailable += OnAudioDataAvailable;
        _audioService.PlaybackCompleted += OnPlaybackCompleted;
        _audioService.ProcessingError += OnAudioProcessingError;
    }
    
    #region Events
    
    /// <summary>
    /// 会话状态变化事件
    /// </summary>
    public event EventHandler<SessionStatusChangedEventArgs>? SessionStatusChanged;
    
    /// <summary>
    /// 服务器响应事件
    /// </summary>
    public event EventHandler<ServerResponseEventArgs>? ServerResponseReceived;
    
    /// <summary>
    /// 音频数据事件
    /// </summary>
    public event EventHandler<AudioDataEventArgs>? AudioDataReceived;
    
    /// <summary>
    /// 错误事件
    /// </summary>
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;
    
    #endregion
    
    #region Session Management
    
    /// <summary>
    /// 创建会话
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="config">会话配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息</returns>
    public async Task<DialogSession> CreateSessionAsync(string userId, SessionConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sessionLock.WaitAsync(cancellationToken);
            
            // 检查并发会话限制
            if (_sessions.Count >= _options.MaxConcurrentSessions)
            {
                throw new InvalidOperationException($"已达到最大并发会话数限制: {_options.MaxConcurrentSessions}");
            }
            
            var sessionId = Guid.NewGuid().ToString();
            var session = new DialogSession
            {
                SessionId = sessionId,
                Config = config,
                Status = SessionStatus.Created,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                Messages = new List<SessionMessage>()
            };
            
            _sessions.TryAdd(sessionId, session);
            
            // 创建音频会话
            _audioService.CreateSession(sessionId, config.AudioConfig);
            
            OnSessionStatusChanged(new SessionStatusChangedEventArgs
            {
                SessionId = sessionId,
                OldStatus = SessionStatus.None,
                NewStatus = SessionStatus.Created,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            _logger.LogInformation("创建会话: {SessionId}", sessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            throw;
        }
        finally
        {
            _sessionLock.Release();
        }
    }
    
    /// <summary>
    /// 获取会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息</returns>
    public Task<DialogSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }
    
    /// <summary>
    /// 开始会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>开始任务</returns>
    public async Task StartSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
            {
                throw new ArgumentException($"会话不存在: {sessionId}");
            }
            
            if (session.Status != SessionStatus.Created)
            {
                throw new InvalidOperationException($"会话状态无效: {session.Status}");
            }
            
            // 建立WebSocket连接
            var connected = await _webSocketManager.CreateConnectionAsync(sessionId, cancellationToken);
            if (!connected)
            {
                throw new InvalidOperationException("建立WebSocket连接失败");
            }
            
            // 初始化音频服务
            await _audioService.InitializeAsync(session.Config.AudioConfig, cancellationToken);
            
            // 发送会话控制消息
            var sessionControlMessage = new SessionControl
            {
                Header = CreateMessageHeader(MessageType.SessionControl),
                Action = "start",
                SessionId = sessionId,
                Config = session.Config
            };
            
            await _webSocketManager.SendMessageAsync(sessionId, sessionControlMessage, cancellationToken);
            
            session.Status = SessionStatus.Active;
            session.StartedAt = DateTime.UtcNow;
            session.LastActivityAt = DateTime.UtcNow;
            
            OnSessionStatusChanged(new SessionStatusChangedEventArgs
            {
                SessionId = sessionId,
                OldStatus = SessionStatus.Created,
                NewStatus = SessionStatus.Active,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            _logger.LogInformation("会话已启动: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动会话失败: {SessionId}", sessionId);
            
            OnErrorOccurred(new ErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "SessionStartFailed",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            throw;
        }
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="reason">结束原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结束任务</returns>
    public async Task EndSessionAsync(string sessionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
            {
                return;
            }
            
            // 停止录制
            await _audioService.StopRecordingAsync(sessionId, cancellationToken);
            
            // 发送会话结束消息
            var sessionControlMessage = new SessionControl
            {
                Header = CreateMessageHeader(MessageType.SessionControl),
                Action = "end",
                SessionId = sessionId
            };
            
            await _webSocketManager.SendMessageAsync(sessionId, sessionControlMessage, cancellationToken);
            
            // 关闭WebSocket连接
            await _webSocketManager.CloseConnectionAsync(sessionId, cancellationToken);
            
            // 移除音频会话
            _audioService.RemoveSession(sessionId);
            
            session.Status = SessionStatus.Ended;
            session.EndedAt = DateTime.UtcNow;
            
            OnSessionStatusChanged(new SessionStatusChangedEventArgs
            {
                SessionId = sessionId,
                OldStatus = SessionStatus.Created,
                NewStatus = SessionStatus.Ended,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            _logger.LogInformation("会话已结束: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话失败: {SessionId}", sessionId);
            throw;
        }
    }
    
    /// <summary>
    /// 删除会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除任务</returns>
    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 先结束会话
            await EndSessionAsync(sessionId, cancellationToken);
            
            // 从内存中移除
            _sessions.TryRemove(sessionId, out _);
            
            _logger.LogInformation("会话已删除: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话失败: {SessionId}", sessionId);
            throw;
        }
    }
    
    #endregion
    
    #region Communication
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioChunk">音频块</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    public async Task SendAudioAsync(string sessionId, AudioChunk audioChunk, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || session.Status != SessionStatus.Active)
            {
                throw new InvalidOperationException($"会话不可用: {sessionId}");
            }
            
            var audioRequest = new ClientAudioOnlyRequest
            {
                Header = CreateMessageHeader(MessageType.ClientAudioOnlyRequest),
                SessionId = sessionId,
                AudioData = audioChunk.Data,
                AudioFormat = session.Config.AudioConfig.InputFormat,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await _webSocketManager.SendMessageAsync(sessionId, audioRequest, cancellationToken);
            
            // 记录消息
            session.Messages.Add(new SessionMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = Models.Session.MessageType.Audio,
                TextContent = $"音频数据 ({audioChunk.Data.Length} 字节)",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsUserMessage = true,
                ProcessingStatus = MessageProcessingStatus.Pending
            });
            
            session.LastActivityAt = DateTime.UtcNow;
            
            _logger.LogDebug("发送音频数据: {SessionId}, 大小: {Size} 字节", sessionId, audioChunk.Data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频数据失败: {SessionId}", sessionId);
            
            OnErrorOccurred(new ErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "SendAudioFailed",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            throw;
        }
    }
    
    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="text">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    public async Task SendTextAsync(string sessionId, string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || session.Status != SessionStatus.Active)
            {
                throw new InvalidOperationException($"会话不可用: {sessionId}");
            }
            
            var textRequest = new ClientFullRequestMessage
            {
                Header = CreateMessageHeader(Models.Protocol.MessageType.ClientFullRequest),
                SessionId = sessionId,
                Text = text,
                AudioData = Array.Empty<byte>(),
                AudioFormat = session.Config.AudioConfig.InputFormat,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await _webSocketManager.SendMessageAsync(sessionId, textRequest, cancellationToken);
            
            // 记录消息
            session.Messages.Add(new SessionMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = Models.Session.MessageType.Text,
                TextContent = text,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsUserMessage = true,
                ProcessingStatus = MessageProcessingStatus.Pending
            });
            
            session.LastActivityAt = DateTime.UtcNow;
            
            _logger.LogDebug("发送文本消息: {SessionId}, 内容: {Text}", sessionId, text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送文本消息失败: {SessionId}", sessionId);
            
            OnErrorOccurred(new ErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "SendTextFailed",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            throw;
        }
    }
    
    #endregion
    
    #region Data Retrieval
    
    /// <summary>
    /// 获取会话消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="limit">限制数量</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    public async Task<IEnumerable<SessionMessage>> GetMessagesAsync(string sessionId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            return Enumerable.Empty<SessionMessage>();
        }
        
        return session.Messages
            .OrderByDescending(m => m.Timestamp)
            .Skip(offset)
            .Take(limit);
    }
    
    /// <summary>
    /// 获取用户会话列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="limit">限制数量</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表</returns>
    public Task<IEnumerable<DialogSession>> GetUserSessionsAsync(string userId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit);
        
        return Task.FromResult(sessions);
    }
    
    /// <summary>
    /// 获取会话统计信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计信息</returns>
    public async Task<AudioStatistics?> GetSessionStatisticsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            return null;
        }
        
        var audioStats = _audioService.GetStatistics(sessionId);
        
        return audioStats;
    }
    
    #endregion
    
    #region Private Methods
    
    private MessageHeader CreateMessageHeader(Models.Protocol.MessageType messageType)
    {
        return new MessageHeader
        {
            Version = Convert.ToByte("1.0"),
            MessageType = messageType,
            SerializationMethod = SerializationMethod.Json,
            CompressionMethod = CompressionMethod.Gzip,
            Flags = MessageFlags.None,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    private void CleanupExpiredSessions(object? state)
    {
        try
        {
            var expiredSessions = _sessions.Values
                .Where(s => DateTime.UtcNow - s.LastActivityAt > _options.SessionTimeout)
                .ToList();
            
            foreach (var session in expiredSessions)
            {
                _logger.LogInformation("清理过期会话: {SessionId}", session.SessionId);
                _ = Task.Run(() => DeleteSessionAsync(session.SessionId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期会话时发生错误");
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        var sessionStatus = e.Status switch
        {
            ConnectionStatus.Connected => SessionStatus.Active,
            ConnectionStatus.Disconnected => SessionStatus.Finished,
            _ => SessionStatus.Error
        };
        
        if (_sessions.TryGetValue(e.SessionId, out var session))
        {
            session.Status = sessionStatus;
            session.LastActivityAt = DateTime.UtcNow;
        }
        
        OnSessionStatusChanged(new SessionStatusChangedEventArgs
        {
            SessionId = e.SessionId,
            OldStatus = session?.Status ?? SessionStatus.Created,
            NewStatus = sessionStatus,
            Timestamp = e.Timestamp
        });
    }
    
    private async void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var session = await GetSessionAsync(e.SessionId);
            if (session == null) return;
            
            session.LastActivityAt = DateTime.UtcNow;
            
            // 处理不同类型的消息
            switch (e.Message)
            {
                case ServerFullResponseMessage response:
                    await HandleServerResponse(e.SessionId, response);
                    break;
                    
                case ServerAckMessage ack:
                    await HandleServerAck(e.SessionId, ack);
                    break;
                    
                case ServerErrorResponseMessage error:
                    await HandleServerError(e.SessionId, error);
                    break;
                    
                case TtsTriggerMessage ttsTrigger:
                    await HandleTtsTrigger(e.SessionId, ttsTrigger);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理接收消息失败: {SessionId}", e.SessionId);
        }
    }
    
    private async Task HandleServerResponse(string sessionId, ServerFullResponseMessage response)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null) return;
        
        // 记录响应消息
        session.Messages.Add(new SessionMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = Models.Session.MessageType.Text,
            TextContent = response.ResponseText ?? string.Empty,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsUserMessage = false,
            ProcessingStatus = MessageProcessingStatus.Completed
        });
        
        // 播放音频响应
        if (!string.IsNullOrEmpty(response.TtsAudio))
        {
            var audioData = Convert.FromBase64String(response.TtsAudio);
            var audioChunk = new AudioChunk
            {
                Data = audioData,
                Format = response.AudioFormat ?? session.Config.AudioConfig.OutputFormat,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SequenceNumber = (uint)session.Messages.Count
            };
            
            await _audioService.PlayAudioAsync(sessionId, audioChunk);
        }
        
        OnServerResponseReceived(new ServerResponseEventArgs
        {
            SessionId = sessionId,
            Message = response,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }
    
    private async Task HandleServerAck(string sessionId, ServerAckMessage ack)
    {
        _logger.LogDebug("收到服务器确认: {SessionId}, 状态: {Status}", sessionId, ack.Status);
        
        // 更新消息状态
        var session = await GetSessionAsync(sessionId);
        if (session != null && ack.AckMessageId != null)
        {
            var message = session.Messages.FirstOrDefault(m => m.MessageId == ack.AckMessageId);
            if (message != null)
            {
                message.ProcessingStatus = ack.Status == "success" 
                    ? MessageProcessingStatus.Completed 
                    : MessageProcessingStatus.Failed;
            }
        }
    }
    
    private async Task HandleServerError(string sessionId, ServerErrorResponseMessage error)
    {
        _logger.LogError("收到服务器错误: {SessionId}, 错误码: {ErrorCode}, 消息: {ErrorMessage}", 
            sessionId, error.ErrorCode, error.ErrorMessage);
        
        OnErrorOccurred(new ErrorEventArgs
        {
            SessionId = sessionId,
            ErrorType = "ServerError",
            ErrorMessage = $"[{error.ErrorCode}] {error.ErrorMessage}",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }
    
    private async Task HandleTtsTrigger(string sessionId, TtsTriggerMessage ttsTrigger)
    {
        _logger.LogDebug("收到TTS触发: {SessionId}, 文本: {Text}", sessionId, ttsTrigger.TriggerText);
        
        // 可以在这里实现自定义的TTS处理逻辑
    }
    
    private void OnConnectionError(object? sender, ConnectionErrorEventArgs e)
    {
        OnErrorOccurred(new ErrorEventArgs
        {
            SessionId = e.SessionId,
            ErrorType = e.ErrorType,
            ErrorMessage = e.ErrorMessage,
            Exception = e.Exception,
            Timestamp = e.Timestamp
        });
    }
    
    private void OnAudioDataAvailable(object? sender, AudioDataAvailableEventArgs e)
    {
        OnAudioDataReceived(new AudioDataEventArgs
        {
            SessionId = string.Empty, // 需要从上下文获取SessionId
            AudioChunk = e.AudioChunk,
            IsInput = true,
            Timestamp = e.RecordedAt
        });
    }
    
    private void OnPlaybackCompleted(object? sender, PlaybackCompletedEventArgs e)
    {
        _logger.LogDebug("音频播放完成");
    }
    
    private void OnAudioProcessingError(object? sender, AudioProcessingErrorEventArgs e)
    {
        OnErrorOccurred(new ErrorEventArgs
        {
            SessionId = e.SessionId,
            ErrorType = e.ErrorType,
            ErrorMessage = e.ErrorMessage,
            Exception = e.Exception,
            Timestamp = e.Timestamp
        });
    }
    
    private void OnSessionStatusChanged(SessionStatusChangedEventArgs e)
    {
        SessionStatusChanged?.Invoke(this, e);
    }
    
    private void OnServerResponseReceived(ServerResponseEventArgs e)
    {
        ServerResponseReceived?.Invoke(this, e);
    }
    
    private void OnAudioDataReceived(AudioDataEventArgs e)
    {
        AudioDataReceived?.Invoke(this, e);
    }
    
    private void OnErrorOccurred(ErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(this, e);
    }
    
    #endregion
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogInformation("释放实时对话服务资源");
        
        // 结束所有活跃会话
        var activeSessions = _sessions.Values
            .Where(s => s.Status == SessionStatus.Active)
            .Select(s => s.SessionId)
            .ToList();
        
        var tasks = activeSessions.Select(sessionId => EndSessionAsync(sessionId)).ToArray();
        
        try
        {
            Task.WaitAll(tasks, TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "结束会话时发生错误");
        }
        
        _sessionCleanupTimer?.Dispose();
        _sessionLock?.Dispose();
        _webSocketManager?.Dispose();
        _audioService?.Dispose();
        
        _disposed = true;
    }
}