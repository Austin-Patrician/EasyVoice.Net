using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EasyVoice.RealtimeDialog.Models.Protocol;
using EasyVoice.RealtimeDialog.Models.Session;
using EasyVoice.RealtimeDialog.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// WebSocket客户端管理器
/// </summary>
public class WebSocketClientManager : IDisposable
{
    private readonly ILogger<WebSocketClientManager> _logger;
    private readonly DoubaoProtocolHandler _protocolHandler;
    private readonly RealtimeDialogOptions _options;
    
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections;
    private readonly SemaphoreSlim _connectionLock;
    private readonly Timer _heartbeatTimer;
    private readonly Timer _cleanupTimer;
    
    private bool _disposed;
    
    public WebSocketClientManager(
        ILogger<WebSocketClientManager> logger,
        DoubaoProtocolHandler protocolHandler,
        IOptions<RealtimeDialogOptions> options)
    {
        _logger = logger;
        _protocolHandler = protocolHandler;
        _options = options.Value;
        
        _connections = new ConcurrentDictionary<string, WebSocketConnection>();
        _connectionLock = new SemaphoreSlim(1, 1);
        
        // 心跳定时器，每30秒发送一次心跳
        _heartbeatTimer = new Timer(SendHeartbeats, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        // 清理定时器，每分钟清理一次过期连接
        _cleanupTimer = new Timer(CleanupExpiredConnections, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
    
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    
    /// <summary>
    /// 消息接收事件
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    
    /// <summary>
    /// 连接错误事件
    /// </summary>
    public event EventHandler<ConnectionErrorEventArgs>? ConnectionError;
    
    /// <summary>
    /// 创建WebSocket连接
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接任务</returns>
    public async Task<bool> CreateConnectionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectionLock.WaitAsync(cancellationToken);
            
            if (_connections.ContainsKey(sessionId))
            {
                _logger.LogWarning("会话已存在WebSocket连接: {SessionId}", sessionId);
                return false;
            }
            
            var clientWebSocket = new ClientWebSocket();
            
            // 设置请求头
            clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {_options.DoubaoApiKey}");
            clientWebSocket.Options.SetRequestHeader("X-Session-Id", sessionId);
            
            // 连接到WebSocket服务器
            var uri = new Uri(_options.WebSocketEndpoint);
            await clientWebSocket.ConnectAsync(uri, cancellationToken);
            
            var connection = new WebSocketConnection
            {
                SessionId = sessionId,
                WebSocket = clientWebSocket,
                State = WebSocketState.Open,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                SequenceNumber = 0
            };
            
            _connections.TryAdd(sessionId, connection);
            
            // 启动消息接收任务
            _ = Task.Run(() => ReceiveMessagesAsync(connection, cancellationToken), cancellationToken);
            
            OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs
            {
                SessionId = sessionId,
                Status = ConnectionStatus.Connected,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            _logger.LogInformation("WebSocket连接已建立: {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建WebSocket连接失败: {SessionId}", sessionId);
            
            OnConnectionError(new ConnectionErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "ConnectionFailed",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    /// <summary>
    /// 关闭WebSocket连接
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>关闭任务</returns>
    public async Task<bool> CloseConnectionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_connections.TryRemove(sessionId, out var connection))
            {
                _logger.LogWarning("WebSocket连接不存在: {SessionId}", sessionId);
                return false;
            }
            
            if (connection.WebSocket.State == WebSocketState.Open)
            {
                await connection.WebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended",
                    cancellationToken);
            }
            
            connection.WebSocket.Dispose();
            connection.State = WebSocketState.Closed;
            
            OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs
            {
                SessionId = sessionId,
                Status = ConnectionStatus.Disconnected,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            _logger.LogInformation("WebSocket连接已关闭: {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭WebSocket连接失败: {SessionId}", sessionId);
            return false;
        }
    }
    
    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="message">协议消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    public async Task<bool> SendMessageAsync(string sessionId, ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_connections.TryGetValue(sessionId, out var connection))
            {
                _logger.LogWarning("WebSocket连接不存在: {SessionId}", sessionId);
                return false;
            }
            
            if (connection.WebSocket.State != WebSocketState.Open)
            {
                _logger.LogWarning("WebSocket连接未打开: {SessionId}, 状态: {State}", sessionId, connection.WebSocket.State);
                return false;
            }
            
            // 设置消息头
            message.Header.SequenceNumber = ++connection.SequenceNumber;
            message.Header.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // 序列化消息
            var messageData = _protocolHandler.SerializeMessage(message);
            
            // 发送消息
            await connection.WebSocket.SendAsync(
                new ArraySegment<byte>(messageData),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
            
            connection.LastActivity = DateTime.UtcNow;
            connection.MessagesSent++;
            
            _logger.LogDebug("发送消息: {SessionId}, 类型: {MessageType}, 序列号: {SequenceNumber}",
                sessionId, message.Header.MessageType, message.Header.SequenceNumber);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息失败: {SessionId}", sessionId);
            
            OnConnectionError(new ConnectionErrorEventArgs
            {
                SessionId = sessionId,
                ErrorType = "SendMessageFailed",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            
            return false;
        }
    }
    
    /// <summary>
    /// 发送心跳消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    public async Task<bool> SendHeartbeatAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var heartbeatMessage = new Heartbeat
        {
            Header = new MessageHeader
            {
                Version = "1.0",
                MessageType = MessageType.Heartbeat,
                SerializationMethod = SerializationMethod.Json,
                CompressionMethod = CompressionMethod.None,
                Flags = MessageFlags.None
            },
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClientId = sessionId
        };
        
        return await SendMessageAsync(sessionId, heartbeatMessage, cancellationToken);
    }
    
    /// <summary>
    /// 获取连接状态
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>连接状态</returns>
    public ConnectionStatus GetConnectionStatus(string sessionId)
    {
        if (_connections.TryGetValue(sessionId, out var connection))
        {
            return connection.WebSocket.State switch
            {
                WebSocketState.Open => ConnectionStatus.Connected,
                WebSocketState.Connecting => ConnectionStatus.Connecting,
                WebSocketState.CloseSent or WebSocketState.CloseReceived => ConnectionStatus.Disconnecting,
                _ => ConnectionStatus.Disconnected
            };
        }
        
        return ConnectionStatus.Disconnected;
    }
    
    /// <summary>
    /// 获取连接统计信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>连接统计</returns>
    public ConnectionStatistics? GetConnectionStatistics(string sessionId)
    {
        if (_connections.TryGetValue(sessionId, out var connection))
        {
            return new ConnectionStatistics
            {
                SessionId = sessionId,
                ConnectedAt = connection.ConnectedAt,
                LastActivity = connection.LastActivity,
                MessagesSent = connection.MessagesSent,
                MessagesReceived = connection.MessagesReceived,
                BytesSent = connection.BytesSent,
                BytesReceived = connection.BytesReceived,
                ConnectionDuration = DateTime.UtcNow - connection.ConnectedAt
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// 获取所有活跃连接
    /// </summary>
    /// <returns>活跃连接列表</returns>
    public IEnumerable<string> GetActiveConnections()
    {
        return _connections.Values
            .Where(c => c.WebSocket.State == WebSocketState.Open)
            .Select(c => c.SessionId);
    }
    
    #region Private Methods
    
    private async Task ReceiveMessagesAsync(WebSocketConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();
        
        try
        {
            while (connection.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await connection.WebSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket连接关闭: {SessionId}", connection.SessionId);
                    break;
                }
                
                messageBuffer.AddRange(buffer.Take(result.Count));
                
                if (result.EndOfMessage)
                {
                    await ProcessReceivedMessage(connection, messageBuffer.ToArray());
                    messageBuffer.Clear();
                }
                
                connection.LastActivity = DateTime.UtcNow;
                connection.MessagesReceived++;
                connection.BytesReceived += result.Count;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("WebSocket消息接收已取消: {SessionId}", connection.SessionId);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket连接错误: {SessionId}", connection.SessionId);
            
            OnConnectionError(new ConnectionErrorEventArgs
            {
                SessionId = connection.SessionId,
                ErrorType = "WebSocketError",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接收WebSocket消息时发生错误: {SessionId}", connection.SessionId);
        }
        finally
        {
            // 连接断开时清理
            _connections.TryRemove(connection.SessionId, out _);
            
            OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs
            {
                SessionId = connection.SessionId,
                Status = ConnectionStatus.Disconnected,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }
    
    private async Task ProcessReceivedMessage(WebSocketConnection connection, byte[] messageData)
    {
        try
        {
            var message = await _protocolHandler.DeserializeMessageAsync(messageData);
            
            _logger.LogDebug("接收消息: {SessionId}, 类型: {MessageType}, 序列号: {SequenceNumber}",
                connection.SessionId, message.Header.MessageType, message.Header.SequenceNumber);
            
            OnMessageReceived(new MessageReceivedEventArgs
            {
                SessionId = connection.SessionId,
                Message = message,
                ReceivedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理接收消息失败: {SessionId}", connection.SessionId);
            
            OnConnectionError(new ConnectionErrorEventArgs
            {
                SessionId = connection.SessionId,
                ErrorType = "MessageProcessingFailed",
                ErrorMessage = ex.Message,
                Exception = ex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }
    
    private void SendHeartbeats(object? state)
    {
        try
        {
            var activeSessions = GetActiveConnections().ToList();
            
            foreach (var sessionId in activeSessions)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendHeartbeatAsync(sessionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "发送心跳失败: {SessionId}", sessionId);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送心跳时发生错误");
        }
    }
    
    private void CleanupExpiredConnections(object? state)
    {
        try
        {
            var expiredConnections = _connections.Values
                .Where(c => DateTime.UtcNow - c.LastActivity > _options.SessionTimeout)
                .ToList();
            
            foreach (var connection in expiredConnections)
            {
                _logger.LogInformation("清理过期连接: {SessionId}", connection.SessionId);
                _ = Task.Run(() => CloseConnectionAsync(connection.SessionId));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期连接时发生错误");
        }
    }
    
    private void OnConnectionStatusChanged(ConnectionStatusChangedEventArgs e)
    {
        ConnectionStatusChanged?.Invoke(this, e);
    }
    
    private void OnMessageReceived(MessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, e);
    }
    
    private void OnConnectionError(ConnectionErrorEventArgs e)
    {
        ConnectionError?.Invoke(this, e);
    }
    
    #endregion
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogInformation("释放WebSocket客户端管理器资源");
        
        // 关闭所有连接
        var tasks = _connections.Keys.Select(sessionId => CloseConnectionAsync(sessionId)).ToArray();
        
        try
        {
            Task.WaitAll(tasks, TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关闭WebSocket连接时发生错误");
        }
        
        _heartbeatTimer?.Dispose();
        _cleanupTimer?.Dispose();
        _connectionLock?.Dispose();
        
        _disposed = true;
    }
}

/// <summary>
/// WebSocket连接
/// </summary>
public class WebSocketConnection
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// WebSocket客户端
    /// </summary>
    public ClientWebSocket WebSocket { get; set; } = null!;
    
    /// <summary>
    /// 连接状态
    /// </summary>
    public WebSocketState State { get; set; }
    
    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectedAt { get; set; }
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivity { get; set; }
    
    /// <summary>
    /// 序列号
    /// </summary>
    public uint SequenceNumber { get; set; }
    
    /// <summary>
    /// 发送消息数
    /// </summary>
    public long MessagesSent { get; set; }
    
    /// <summary>
    /// 接收消息数
    /// </summary>
    public long MessagesReceived { get; set; }
    
    /// <summary>
    /// 发送字节数
    /// </summary>
    public long BytesSent { get; set; }
    
    /// <summary>
    /// 接收字节数
    /// </summary>
    public long BytesReceived { get; set; }
}

/// <summary>
/// 连接状态
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// 已断开
    /// </summary>
    Disconnected,
    
    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,
    
    /// <summary>
    /// 已连接
    /// </summary>
    Connected,
    
    /// <summary>
    /// 断开中
    /// </summary>
    Disconnecting
}

/// <summary>
/// 连接状态变化事件参数
/// </summary>
public class ConnectionStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 连接状态
    /// </summary>
    public ConnectionStatus Status { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 消息接收事件参数
/// </summary>
public class MessageReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 协议消息
    /// </summary>
    public ProtocolMessage Message { get; set; } = null!;
    
    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime ReceivedAt { get; set; }
}

/// <summary>
/// 连接错误事件参数
/// </summary>
public class ConnectionErrorEventArgs : EventArgs
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
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 连接统计信息
/// </summary>
public class ConnectionStatistics
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectedAt { get; set; }
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivity { get; set; }
    
    /// <summary>
    /// 发送消息数
    /// </summary>
    public long MessagesSent { get; set; }
    
    /// <summary>
    /// 接收消息数
    /// </summary>
    public long MessagesReceived { get; set; }
    
    /// <summary>
    /// 发送字节数
    /// </summary>
    public long BytesSent { get; set; }
    
    /// <summary>
    /// 接收字节数
    /// </summary>
    public long BytesReceived { get; set; }
    
    /// <summary>
    /// 连接持续时间
    /// </summary>
    public TimeSpan ConnectionDuration { get; set; }
}