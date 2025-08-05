using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Newtonsoft.Json;

namespace EasyVoice.Core.Services;

/// <summary>
/// Doubao实时语音对话服务实现
/// </summary>
public class DoubaoRealTimeService : IRealTimeService
{
    private readonly ILogger<DoubaoRealTimeService> _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private RealTimeConnectionConfig? _config;
    private SessionInfo? _currentSession;
    private RealTimeDialogState _connectionState = RealTimeDialogState.Disconnected;
    private readonly object _stateLock = new();

    // 音频相关
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    private readonly ConcurrentQueue<byte[]> _audioBuffer = new();

    // 协议相关
    private int _messageSequence = 0;
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingRequests = new();

    public DoubaoRealTimeService(ILogger<DoubaoRealTimeService> logger)
    {
        _logger = logger;
    }

    #region Events

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<AudioDataEventArgs>? AudioDataReceived;
    public event EventHandler<DialogEventArgs>? DialogEvent;
    public event EventHandler<Models.ErrorEventArgs>? ErrorOccurred;

    #endregion

    #region Audio Processing

    public async Task StartAudioRecordingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_waveIn != null)
            {
                _logger.LogWarning("音频录制已在进行中");
                return;
            }

            if (_config == null)
            {
                _logger.LogError("连接配置未设置");
                return;
            }

            // 初始化音频录制
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(_config.InputSampleRate, 1),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += async (sender, e) =>
            {
                try
                {
                    // 发送音频数据到服务器
                    var audioData = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, audioData, e.BytesRecorded);

                    await SendAudioDataAsync(sessionId, audioData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理录制音频数据时发生错误");
                }
            };

            _waveIn.RecordingStopped += (sender, e) => { _logger.LogInformation("音频录制已停止"); };

            _waveIn.StartRecording();
            _logger.LogInformation("开始音频录制，会话ID: {SessionId}", sessionId);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动音频录制时发生错误");
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "启动音频录制失败", Exception = ex });
        }
    }

    public async Task StopAudioRecordingAsync()
    {
        try
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
                _logger.LogInformation("音频录制已停止");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频录制时发生错误");
        }
    }

    public async Task StartAudioPlaybackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_waveOut != null)
            {
                _logger.LogWarning("音频播放已在进行中");
                return;
            }

            if (_config == null)
            {
                _logger.LogError("连接配置未设置");
                return;
            }

            // 初始化音频播放
            var waveFormat = new WaveFormat(_config.OutputSampleRate, 1);
            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(_config.AudioBufferSeconds * 1000 * 2),
                DiscardOnBufferOverflow = true
            };

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_waveProvider);
            _waveOut.Play();

            // 启动音频缓冲处理任务
            _ = Task.Run(() => ProcessAudioBufferAsync(cancellationToken), cancellationToken);

            _logger.LogInformation("开始音频播放");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动音频播放时发生错误");
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "启动音频播放失败", Exception = ex });
        }
    }

    public async Task StopAudioPlaybackAsync()
    {
        try
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
                _logger.LogInformation("音频播放已停止");
            }

            _waveProvider = null;

            // 清空音频缓冲区
            while (_audioBuffer.TryDequeue(out _))
            {
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频播放时发生错误");
        }
    }

    private async Task ProcessAudioBufferAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _waveProvider != null)
            {
                if (_audioBuffer.TryDequeue(out var audioData))
                {
                    _waveProvider.AddSamples(audioData, 0, audioData.Length);
                }
                else
                {
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("音频缓冲处理任务被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理音频缓冲时发生错误");
        }
    }

    #endregion

    #region Properties

    public RealTimeDialogState ConnectionState
    {
        get
        {
            lock (_stateLock)
            {
                return _connectionState;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                if (_connectionState != value)
                {
                    var oldState = _connectionState;
                    _connectionState = value;
                    ConnectionStateChanged?.Invoke(this,
                        new ConnectionStateChangedEventArgs { OldState = oldState, NewState = value });
                    _logger.LogInformation("连接状态从 {OldState} 变更为 {NewState}", oldState, value);
                }
            }
        }
    }

    public SessionInfo? CurrentSession => _currentSession;

    #endregion

    #region Connection Management

    public async Task<bool> ConnectAsync(RealTimeConnectionConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            if (ConnectionState != RealTimeDialogState.Disconnected)
            {
                _logger.LogWarning("连接已存在，当前状态: {State}", ConnectionState);
                return false;
            }

            _config = config;
            ConnectionState = RealTimeDialogState.Connecting;

            _cancellationTokenSource = new CancellationTokenSource();
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token).Token;

            _webSocket = new ClientWebSocket();

            // 设置WebSocket选项
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            
            // 添加Doubao API认证头部（与Go实现保持一致）
            _webSocket.Options.SetRequestHeader("X-Api-Resource-Id", "volc.speech.dialog");
            _webSocket.Options.SetRequestHeader("X-Api-Access-Key", config.AccessToken);
            _webSocket.Options.SetRequestHeader("X-Api-App-Key", "PlgvMymc7f3tQnJ6");
            _webSocket.Options.SetRequestHeader("X-Api-App-ID", config.AppId);
            _webSocket.Options.SetRequestHeader("X-Api-Connect-Id", Guid.NewGuid().ToString());

            // 连接到WebSocket
            var uri = new Uri(config.WebSocketUrl);
            await _webSocket.ConnectAsync(uri, combinedToken);

            if (_webSocket.State == WebSocketState.Open)
            {
                ConnectionState = RealTimeDialogState.Connected;

                // 启动消息接收循环
                _ = Task.Run(() => ReceiveMessagesAsync(combinedToken), combinedToken);

                _logger.LogInformation("成功连接到Doubao实时语音服务: {Url}", config.WebSocketUrl);
                return true;
            }

            _logger.LogError("WebSocket连接失败，状态: {State}", _webSocket.State);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接到Doubao实时语音服务时发生错误");
            ConnectionState = RealTimeDialogState.Disconnected;
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "连接失败", Exception = ex });
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ConnectionState = RealTimeDialogState.Disconnecting;

            // 停止音频
            await StopAudioRecordingAsync();
            await StopAudioPlaybackAsync();

            // 取消所有操作
            _cancellationTokenSource?.Cancel();

            // 关闭WebSocket
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常关闭", cancellationToken);
            }

            ConnectionState = RealTimeDialogState.Disconnected;
            _currentSession = null;

            _logger.LogInformation("已断开与Doubao实时语音服务的连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开连接时发生错误");
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "断开连接失败", Exception = ex });
        }
    }

    #endregion

    #region Session Management

    public async Task<bool> StartSessionAsync(string sessionId, StartSessionPayload payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (ConnectionState != RealTimeDialogState.Connected)
            {
                _logger.LogWarning("WebSocket未连接，无法开始会话");
                return false;
            }

            var payloadJson = JsonConvert.SerializeObject(new
            {
                event_type = RealTimeEventType.StartSession,
                session_id = sessionId,
                payload = payload
            });
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            var message = new ProtocolMessage
            {
                Type = MessageType.FullClient,
                Sequence = Interlocked.Increment(ref _messageSequence),
                Payload = payloadBytes
            };

            var success = await SendProtocolMessageAsync(message, cancellationToken);
            if (success)
            {
                _currentSession = new SessionInfo
                {
                    SessionId = sessionId,
                    CreatedAt = DateTime.UtcNow,
                    State = RealTimeDialogState.InSession
                };

                ConnectionState = RealTimeDialogState.InSession;
                _logger.LogInformation("会话已开始，会话ID: {SessionId}", sessionId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始会话时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "开始会话失败", Exception = ex });
            return false;
        }
    }

    public async Task<bool> FinishSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentSession?.SessionId != sessionId)
            {
                _logger.LogWarning("会话ID不匹配，无法结束会话: {SessionId}", sessionId);
                return false;
            }

            var payloadJson = JsonConvert.SerializeObject(new
            {
                event_type = RealTimeEventType.FinishSession,
                session_id = sessionId
            });
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            var message = new ProtocolMessage
            {
                Type = MessageType.FullClient,
                Sequence = Interlocked.Increment(ref _messageSequence),
                Payload = payloadBytes
            };

            var success = await SendProtocolMessageAsync(message, cancellationToken);
            if (success)
            {
                if (_currentSession != null)
                {
                    _currentSession.State = RealTimeDialogState.Disconnected;
                    _currentSession.LastActiveAt = DateTime.UtcNow;
                }

                ConnectionState = RealTimeDialogState.Connected;
                _logger.LogInformation("会话已结束，会话ID: {SessionId}", sessionId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "结束会话失败", Exception = ex });
            return false;
        }
    }

    public async Task<bool> SayHelloAsync(string sessionId, SayHelloPayload payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentSession?.SessionId != sessionId)
            {
                _logger.LogWarning("会话ID不匹配，无法发送问候语: {SessionId}", sessionId);
                return false;
            }

            var payloadJson = JsonConvert.SerializeObject(new
            {
                event_type = RealTimeEventType.SayHello,
                session_id = sessionId,
                payload = payload
            });
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            var message = new ProtocolMessage
            {
                Type = MessageType.FullClient,
                Sequence = Interlocked.Increment(ref _messageSequence),
                Payload = payloadBytes
            };

            var success = await SendProtocolMessageAsync(message, cancellationToken);
            if (success)
            {
                _logger.LogInformation("问候语已发送，会话ID: {SessionId}", sessionId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送问候语时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "发送问候语失败", Exception = ex });
            return false;
        }
    }

    public async Task<bool> SendChatTtsTextAsync(string sessionId, ChatTtsTextPayload payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentSession?.SessionId != sessionId)
            {
                _logger.LogWarning("会话ID不匹配，无法发送聊天TTS文本: {SessionId}", sessionId);
                return false;
            }

            var payloadJson = JsonConvert.SerializeObject(new
            {
                event_type = RealTimeEventType.ChatTtsText,
                session_id = sessionId,
                payload = payload
            });
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            var message = new ProtocolMessage
            {
                Type = MessageType.FullClient,
                Sequence = Interlocked.Increment(ref _messageSequence),
                Payload = payloadBytes
            };

            var success = await SendProtocolMessageAsync(message, cancellationToken);
            if (success)
            {
                _logger.LogInformation("聊天TTS文本已发送，会话ID: {SessionId}, 文本: {Text}", sessionId, payload.Content);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送聊天TTS文本时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "发送聊天TTS文本失败", Exception = ex });
            return false;
        }
    }

    public async Task<bool> SendAudioDataAsync(string sessionId, byte[] audioData, int? sequence = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentSession?.SessionId != sessionId)
            {
                _logger.LogWarning("会话ID不匹配，无法发送音频数据: {SessionId}", sessionId);
                return false;
            }

            var message = new ProtocolMessage
            {
                Type = MessageType.AudioOnlyClient,
                Sequence = sequence ?? Interlocked.Increment(ref _messageSequence),
                Payload = audioData
            };

            var success = await SendProtocolMessageAsync(message, cancellationToken);
            if (success)
            {
                _logger.LogDebug("音频数据已发送，会话ID: {SessionId}, 数据长度: {Length}", sessionId, audioData.Length);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频数据时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "发送音频数据失败", Exception = ex });
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetConnectionStatsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["ConnectionState"] = ConnectionState.ToString(),
            ["WebSocketState"] = _webSocket?.State.ToString() ?? "None",
            ["CurrentSession"] = _currentSession?.SessionId ?? "None",
            ["MessageSequence"] = _messageSequence,
            ["AudioBufferCount"] = _audioBuffer.Count,
            ["IsRecording"] = _waveIn != null,
            ["IsPlaying"] = _waveOut?.PlaybackState.ToString() ?? "Stopped"
        };

        if (_currentSession != null)
        {
            stats["SessionStartTime"] = _currentSession.CreatedAt;
            stats["SessionDuration"] = DateTime.UtcNow - _currentSession.CreatedAt;
            stats["SessionActive"] = _currentSession.State == RealTimeDialogState.InSession;
        }

        return await Task.FromResult(stats);
    }

    #endregion

    #region Protocol Handling

    private async Task<bool> SendProtocolMessageAsync(ProtocolMessage message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                _logger.LogWarning("WebSocket未连接，无法发送消息");
                return false;
            }

            var messageBytes = SerializeProtocolMessage(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Binary, true,
                cancellationToken);

            _logger.LogDebug("发送协议消息: {MessageType}, 序列号: {Sequence}", message.Type, message.Sequence);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送协议消息时发生错误");
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "发送消息失败", Exception = ex });
            return false;
        }
    }

    private byte[] SerializeProtocolMessage(ProtocolMessage message)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // 写入协议头
        writer.Write((byte)ProtocolVersion.Version1);
        writer.Write((byte)message.Type);
        writer.Write((byte)SerializationMethod.JSON);
        writer.Write((byte)CompressionMethod.None);
        writer.Write(message.Sequence ?? 0);
        writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // 获取负载数据
        var payloadBytes = message.Payload ?? [];

        // 写入负载长度和数据
        writer.Write(payloadBytes.Length);
        writer.Write(payloadBytes);

        return stream.ToArray();
    }

    private ProtocolMessage? DeserializeProtocolMessage(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            // 读取协议头
            var protocolVersion = (ProtocolVersion)reader.ReadByte();
            var messageType = (MessageType)reader.ReadByte();
            var serializationMethod = (SerializationMethod)reader.ReadByte();
            var compressionMethod = (CompressionMethod)reader.ReadByte();
            var sequence = reader.ReadInt32();
            var timestamp = reader.ReadInt64();

            // 读取负载数据
            var payloadLength = reader.ReadInt32();
            var payloadBytes = reader.ReadBytes(payloadLength);

            // 解压缩处理
            if (compressionMethod == CompressionMethod.Gzip && payloadBytes.Length > 0)
            {
                payloadBytes = DecompressData(payloadBytes);
            }

            // 反序列化负载
            object? payload = null;
            if (payloadBytes.Length > 0)
            {
                switch (serializationMethod)
                {
                    case SerializationMethod.JSON:
                        var jsonString = Encoding.UTF8.GetString(payloadBytes);
                        payload = JsonConvert.DeserializeObject(jsonString);
                        break;
                    case SerializationMethod.Raw:
                        payload = payloadBytes;
                        break;
                }
            }

            return new ProtocolMessage
            {
                Type = messageType,
                Sequence = sequence,
                Payload = payloadBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "反序列化协议消息时发生错误");
            return null;
        }
    }

    private byte[] CompressData(byte[] data)
    {
        // 简化实现，实际应使用GZip压缩
        return data;
    }

    private byte[] DecompressData(byte[] data)
    {
        // 简化实现，实际应使用GZip解压缩
        return data;
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var messageData = new byte[result.Count];
                    Array.Copy(buffer, messageData, result.Count);

                    var message = DeserializeProtocolMessage(messageData);
                    if (message != null)
                    {
                        await ProcessReceivedMessageAsync(message);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket连接被服务器关闭");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("消息接收循环被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接收消息时发生错误");
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "接收消息失败", Exception = ex });
        }
    }

    private async Task ProcessReceivedMessageAsync(ProtocolMessage message)
    {
        try
        {
            _logger.LogDebug("收到协议消息: {MessageType}, 序列号: {Sequence}", message.Type, message.Sequence);

            switch (message.Type)
            {
                case MessageType.FullServer:
                    await HandleFullServerMessageAsync(message);
                    break;
                case MessageType.AudioOnlyServer:
                    await HandleAudioOnlyMessageAsync(message);
                    break;
                case MessageType.Error:
                    await HandleErrorMessageAsync(message);
                    break;
                default:
                    _logger.LogWarning("收到未知类型的消息: {MessageType}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理接收到的消息时发生错误");
            ErrorOccurred?.Invoke(this,
                new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = "处理消息失败", Exception = ex });
        }
    }

    private async Task HandleFullServerMessageAsync(ProtocolMessage message)
    {
        // 处理完整服务器消息
        if (message.Payload != null && message.Payload.Length > 0)
        {
            var jsonPayload = Encoding.UTF8.GetString(message.Payload);
            var eventData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonPayload);
            if (eventData != null)
            {
                DialogEvent?.Invoke(this,
                    new DialogEventArgs { EventType = RealTimeEventType.TtsResponse, Data = eventData });
            }
        }

        await Task.CompletedTask;
    }

    private async Task HandleAudioOnlyMessageAsync(ProtocolMessage message)
    {
        // 处理音频数据消息
        if (message.Payload != null && message.Payload.Length > 0)
        {
            var audioData = message.Payload;
            AudioDataReceived?.Invoke(this, new AudioDataEventArgs { AudioData = audioData });

            // 添加到音频缓冲区
            _audioBuffer.Enqueue(audioData);

            // 如果音频播放器正在运行，播放音频
            if (_waveProvider != null && _waveOut?.PlaybackState == PlaybackState.Playing)
            {
                _waveProvider.AddSamples(audioData, 0, audioData.Length);
            }
        }

        await Task.CompletedTask;
    }

    private async Task HandleErrorMessageAsync(ProtocolMessage message)
    {
        // 处理错误消息
        var errorMessage = message.Payload?.ToString() ?? "未知错误";
        _logger.LogError("收到服务器错误消息: {Error}", errorMessage);
        ErrorOccurred?.Invoke(this, new EasyVoice.Core.Models.ErrorEventArgs { ErrorMessage = errorMessage });
        await Task.CompletedTask;
    }

    #endregion

    #region IDisposable Implementation

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    // 停止音频处理
                    StopAudioRecordingAsync().Wait(1000);
                    StopAudioPlaybackAsync().Wait(1000);

                    // 断开连接
                    DisconnectAsync().Wait(2000);

                    // 释放资源
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();

                    _webSocket?.Dispose();
                    _waveIn?.Dispose();
                    _waveOut?.Dispose();

                    // 清理待处理请求
                    foreach (var request in _pendingRequests.Values)
                    {
                        request.TrySetCanceled();
                    }

                    _pendingRequests.Clear();

                    // 清空音频缓冲区
                    while (_audioBuffer.TryDequeue(out _))
                    {
                    }

                    _logger.LogInformation("DoubaoRealTimeService已释放资源");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "释放DoubaoRealTimeService资源时发生错误");
                }
            }

            _disposed = true;
        }
    }

    ~DoubaoRealTimeService()
    {
        Dispose(false);
    }

    #endregion
}