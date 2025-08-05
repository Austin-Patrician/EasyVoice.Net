using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Newtonsoft.Json;

namespace EasyVoice.Core.Services;

/// <summary>
/// Doubao实时语音对话服务实现 - 基于Python版本重构
/// </summary>
public class DoubaoRealTimeService : IRealTimeService, IDisposable
{
    private readonly ILogger<DoubaoRealTimeService> _logger;
    private RealTimeConnectionConfig? _config;
    private string? _sessionId;
    private string? _logId;
    private RealTimeDialogState _connectionState = RealTimeDialogState.Disconnected;
    private readonly object _stateLock = new();
    private SessionInfo? _currentSession;
    
    // WebSocket客户端
    private RealtimeDialogClient? _client;
    
    // 音频设备管理
    private AudioDeviceManager? _audioDevice;
    
    // 会话状态
    private bool _isRunning = true;
    private bool _isSessionFinished = false;
    private bool _isUserQuerying = false;
    private bool _isSendingChatTtsText = false;
    private readonly ConcurrentQueue<byte[]> _audioBuffer = new();
    
    // 音频播放
    private readonly ConcurrentQueue<byte[]> _audioQueue = new();
    private WaveOutEvent? _outputStream;
    private BufferedWaveProvider? _waveProvider;
    private bool _isPlaying = true;
    private CancellationTokenSource? _cancellationTokenSource;
    
    // 接口属性实现
    public RealTimeDialogState ConnectionState => _connectionState;
    public SessionInfo? CurrentSession => _currentSession;

    public DoubaoRealTimeService(ILogger<DoubaoRealTimeService> logger)
    {
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    #region Events

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<AudioDataEventArgs>? AudioDataReceived;
    public event EventHandler<DialogEventArgs>? DialogEvent;
    public event EventHandler<Models.ErrorEventArgs>? ErrorOccurred;

    #endregion

    #region IRealTimeService Implementation

    public async Task<bool> ConnectAsync(RealTimeConnectionConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            SetConnectionState(RealTimeDialogState.Connecting);
            
            _config = config;
            
            // 初始化音频设备管理器
            _audioDevice = new AudioDeviceManager(
                new AudioConfig
                {
                    Format = "pcm",
                    BitSize = 16,
                    Channels = 1,
                    SampleRate = config.InputSampleRate,
                    Chunk = 3200
                },
                new AudioConfig
                {
                    Format = "pcm",
                    BitSize = 32,
                    Channels = 1,
                    SampleRate = config.OutputSampleRate,
                    Chunk = 3200
                }
            );
            
            // 初始化WebSocket客户端
            var wsConfig = new Dictionary<string, object>
            {
                ["base_url"] = config.WebSocketUrl,
                ["headers"] = new Dictionary<string, string>
                {
                    ["X-Api-App-ID"] = config.AppId,
                    ["X-Api-Access-Key"] = config.AccessToken,
                    ["X-Api-Resource-Id"] = "volc.speech.dialog",
                    ["X-Api-App-Key"] = "PlgvMymc7f3tQnJ6",
                    ["X-Api-Connect-Id"] = Guid.NewGuid().ToString()
                }
            };
            
            _client = new RealtimeDialogClient(wsConfig, _sessionId ?? Guid.NewGuid().ToString());
            
            // 连接WebSocket
            await _client.ConnectAsync();
            _logId = _client.LogId;
            
            SetConnectionState(RealTimeDialogState.Connected);
            _logger.LogInformation("已连接到实时对话服务");
            return true;
        }
        catch (Exception ex)
        {
            SetConnectionState(RealTimeDialogState.Error);
            _logger.LogError(ex, "连接失败");
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = ex.Message, Exception = ex });
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            SetConnectionState(RealTimeDialogState.Disconnecting);
            
            // 停止音频处理
            await StopAudioRecordingAsync();
            await StopAudioPlaybackAsync();
            
            // 结束会话
            if (!string.IsNullOrEmpty(_sessionId))
            {
                await _client?.FinishSessionAsync();
            }
            
            // 关闭连接
            await _client?.FinishConnectionAsync();
            await _client?.CloseAsync();
            
            SetConnectionState(RealTimeDialogState.Disconnected);
            _logger.LogInformation("已断开连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开连接失败");
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = ex.Message, Exception = ex });
        }
    }

    public async Task<string> CreateSessionAsync(RealTimeConnectionConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            _sessionId = Guid.NewGuid().ToString();
            _currentSession = new SessionInfo
            {
                SessionId = _sessionId,
                CreatedAt = DateTime.UtcNow,
                State = RealTimeDialogState.Disconnected
            };
            
            _logger.LogInformation("会话创建成功，会话ID: {SessionId}", _sessionId);
            return _sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话时发生错误");
            throw;
        }
    }

    public async Task<bool> StartSessionAsync(string sessionId, StartSessionPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client == null || _config == null)
            {
                throw new InvalidOperationException("会话未创建或配置无效");
            }

            _sessionId = sessionId;
            
            // 初始化音频播放
            InitializeAudioPlayback();
            
            // 启动接收循环
            _ = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource!.Token), _cancellationTokenSource.Token);
            
            if (_currentSession != null)
            {
                _currentSession.State = RealTimeDialogState.InSession;
            }
            
            _logger.LogInformation("会话启动成功，会话ID: {SessionId}, LogId: {LogId}", sessionId, _logId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动会话时发生错误");
            SetConnectionState(RealTimeDialogState.Error);
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = "启动会话失败", Exception = ex });
            return false;
        }
    }

    public async Task<SessionInfo> GetSessionStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return new SessionInfo
        {
            SessionId = sessionId,
            State = _connectionState,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
    }

    public async Task<bool> SayHelloAsync(string sessionId, SayHelloPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client == null)
            {
                throw new InvalidOperationException("客户端未初始化");
            }

            await _client.SayHelloAsync(payload.Content ?? "你好，我是豆包，有什么可以帮助你的？");
            _logger.LogInformation("Hello消息发送成功，会话ID: {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送Hello消息时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = "发送Hello消息失败", Exception = ex });
            return false;
        }
    }

    /// <summary>
    /// 发送聊天TTS文本
    /// </summary>
    public async Task<bool> SendChatTtsTextAsync(string sessionId, ChatTtsTextPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client == null)
            {
                throw new InvalidOperationException("客户端未初始化");
            }

            await _client.ChatTtsTextAsync(_isUserQuerying, payload.Start, payload.End, payload.Content);
            _logger.LogInformation("聊天TTS文本发送成功，会话ID: {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送聊天TTS文本时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = ex.Message, Exception = ex });
            return false;
        }
    }

    /// <summary>
    /// 发送音频数据
    /// </summary>
    public async Task<bool> SendAudioDataAsync(string sessionId, byte[] audioData, int? sequence = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client == null)
            {
                throw new InvalidOperationException("客户端未初始化");
            }

            await _client.SendAudioDataAsync(audioData);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频数据时发生错误，会话ID: {SessionId}", sessionId);
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = ex.Message, Exception = ex });
            return false;
        }
    }

    /// <summary>
    /// 获取连接统计信息
    /// </summary>
    public async Task<Dictionary<string, object>> GetConnectionStatsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["ConnectionState"] = _connectionState.ToString(),
            ["SessionId"] = _sessionId ?? "None",
            ["IsRunning"] = _isRunning,
            ["IsSessionFinished"] = _isSessionFinished,
            ["IsUserQuerying"] = _isUserQuerying,
            ["IsSendingChatTtsText"] = _isSendingChatTtsText,
            ["AudioQueueCount"] = _audioQueue.Count,
            ["AudioBufferCount"] = _audioBuffer.Count,
            ["LogId"] = _logId ?? "None"
        };
        
        return await Task.FromResult(stats);
    }

    public async Task StartAudioRecordingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_audioDevice == null || _client == null)
            {
                throw new InvalidOperationException("音频设备或客户端未初始化");
            }

            // 按照Python版本：先发送Hello消息
            await _client.SayHelloAsync("你好，我是豆包，有什么可以帮助你的？");
            
            _logger.LogInformation("已打开麦克风，请讲话...");
            
            // 开始音频录制，按照Python版本的逻辑
            await _audioDevice.StartRecordingAsync(async (audioData) =>
            {
                try
                {
                    // 按照Python版本：直接发送音频数据到服务器
                    await _client.SendAudioDataAsync(audioData);
                    
                    // 添加小延迟避免CPU过度使用，与Python版本一致
                    await Task.Delay(10, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送音频数据时发生错误");
                }
            }, _cancellationTokenSource!.Token);
            
            _logger.LogInformation("音频录制已启动，会话ID: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动音频录制时发生错误");
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = "启动音频录制失败", Exception = ex });
        }
    }

    public async Task StopAudioRecordingAsync()
    {
        try
        {
            if (_audioDevice != null)
            {
                await _audioDevice.StopRecordingAsync();
                _logger.LogInformation("音频录制已停止");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频录制时发生错误");
        }
    }

    public async Task StartAudioPlaybackAsync(CancellationToken cancellationToken = default)
    {
        // 音频播放在StartSessionAsync中已初始化
        await Task.CompletedTask;
    }

    public async Task StopAudioPlaybackAsync()
    {
        try
        {
            _isPlaying = false;
            
            if (_outputStream != null)
            {
                _outputStream.Stop();
                _outputStream.Dispose();
                _outputStream = null;
            }
            
            _waveProvider = null;
            
            // 清空音频队列
            while (_audioQueue.TryDequeue(out _)) { }
            
            _logger.LogInformation("音频播放已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频播放时发生错误");
        }
    }

    public async Task<bool> FinishSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            _isRunning = false;
            
            if (_client != null)
            {
                await _client.FinishSessionAsync();
                
                // 等待会话结束
                while (!_isSessionFinished)
                {
                    await Task.Delay(100, cancellationToken);
                }
                
                await _client.FinishConnectionAsync();
                await Task.Delay(100, cancellationToken);
                await _client.CloseAsync();
            }
            
            SetConnectionState(RealTimeDialogState.Disconnected);
            _logger.LogInformation("会话结束成功，会话ID: {SessionId}, LogId: {LogId}", sessionId, _logId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话时发生错误");
            return false;
        }
    }

    public RealTimeDialogState GetConnectionState()
    {
        lock (_stateLock)
        {
            return _connectionState;
        }
    }

    #endregion

    #region Private Methods

    private void SetConnectionState(RealTimeDialogState newState)
    {
        RealTimeDialogState oldState;
        lock (_stateLock)
        {
            oldState = _connectionState;
            _connectionState = newState;
        }

        if (oldState != newState)
        {
            _logger.LogInformation("连接状态变更: {OldState} -> {NewState}", oldState, newState);
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private void InitializeAudioPlayback()
    {
        if (_config == null) return;
        
        try
        {
            var waveFormat = new WaveFormat(_config.OutputSampleRate, 1);
            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(10),
                DiscardOnBufferOverflow = true
            };
            
            _outputStream = new WaveOutEvent();
            _outputStream.Init(_waveProvider);
            _outputStream.Play();
            
            // 启动音频播放线程
            Task.Run(AudioPlayerThreadAsync, _cancellationTokenSource!.Token);
            
            _logger.LogInformation("音频播放初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化音频播放时发生错误");
        }
    }

    private async Task AudioPlayerThreadAsync()
    {
        _logger.LogInformation("音频播放线程已启动");
        
        while (_isPlaying && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                // 从队列获取音频数据，使用超时机制
                if (_audioQueue.TryDequeue(out var audioData))
                {
                    if (audioData != null && _waveProvider != null)
                    {
                        _waveProvider.AddSamples(audioData, 0, audioData.Length);
                    }
                }
                else
                {
                    // 队列为空时等待一小段时间，避免CPU过度使用
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("音频播放线程已取消");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "音频播放错误");
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
        }
        
        _logger.LogInformation("音频播放线程已结束");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                if (_client == null) break;
                
                var response = await _client.ReceiveServerResponseAsync();
                HandleServerResponse(response);
                
                if (response.ContainsKey("event"))
                {
                    var eventCode = Convert.ToInt32(response["event"]);
                    if (eventCode == 152 || eventCode == 153)
                    {
                        _logger.LogInformation("收到会话结束事件: {EventCode}", eventCode);
                        _isSessionFinished = true;
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("接收循环已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接收消息时发生错误");
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = "接收消息失败", Exception = ex });
        }
    }

    private void HandleServerResponse(Dictionary<string, object> response)
    {
        if (response.Count == 0) return;
        
        try
        {
            var messageType = response.GetValueOrDefault("message_type")?.ToString();
            
            switch (messageType)
            {
                case "SERVER_ACK":
                    if (response.TryGetValue("payload_msg", out var audioPayload) && audioPayload is byte[] audioData)
                    {
                        // 按照Python版本逻辑：如果正在发送ChatTtsText则忽略音频数据
                        if (_isSendingChatTtsText)
                        {
                            return;
                        }
                        
                        // 将音频数据加入队列和缓冲区
                        _audioQueue.Enqueue(audioData);
                        _audioBuffer.Enqueue(audioData);
                        AudioDataReceived?.Invoke(this, new AudioDataEventArgs 
                        { 
                            AudioData = audioData,
                            Format = "pcm",
                            SampleRate = _config?.OutputSampleRate ?? 24000,
                            Channels = 1,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;
                    
                case "SERVER_FULL_RESPONSE":
                    _logger.LogInformation("服务器响应: {Response}", JsonConvert.SerializeObject(response));
                    
                    if (response.TryGetValue("event", out var eventObj))
                    {
                        var eventCode = Convert.ToInt32(eventObj);
                        var payloadMsg = response.GetValueOrDefault("payload_msg") as Dictionary<string, object>;
                        
                        switch (eventCode)
                        {
                            case 450:
                                // 按照Python版本：清空音频队列缓存，设置用户查询状态
                                _logger.LogInformation("清空缓存音频: {SessionId}", response.GetValueOrDefault("session_id"));
                                while (_audioQueue.TryDequeue(out _)) { }
                                _isUserQuerying = true;
                                break;
                                
                            case 350:
                                // 按照Python版本：处理TTS响应事件
                                if (_isSendingChatTtsText && payloadMsg?.GetValueOrDefault("tts_type")?.ToString() == "chat_tts_text")
                                {
                                    while (_audioQueue.TryDequeue(out _)) { }
                                    _isSendingChatTtsText = false;
                                }
                                break;
                                
                            case 459:
                                // 按照Python版本：用户查询结束，随机触发ChatTtsText
                                _isUserQuerying = false;
                                if (new Random().Next(0, 2) == 0)
                                {
                                    _isSendingChatTtsText = true;
                                    _ = Task.Run(TriggerChatTtsTextAsync);
                                }
                                break;
                        }
                        
                        DialogEvent?.Invoke(this, new DialogEventArgs
                        {
                            EventType = (RealTimeEventType)eventCode,
                            Data = payloadMsg,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;
                    
                case "SERVER_ERROR":
                    var errorMsg = response.GetValueOrDefault("payload_msg")?.ToString() ?? "未知错误";
                    _logger.LogError("服务器错误: {ErrorMessage}", errorMsg);
                    ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs { ErrorMessage = errorMsg });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理服务器响应时发生错误");
        }
    }

    private async Task TriggerChatTtsTextAsync()
    {
        try
        {
            if (_client == null) return;
            
            _logger.LogInformation("hit ChatTTSText event, start sending...");
            
            // 按照Python版本的完全相同逻辑和文本内容
            await _client.ChatTtsTextAsync(
                _isUserQuerying, 
                true, 
                false, 
                "这是第一轮TTS的开始和中间包事件，这两个合而为一了。"
            );
            
            await _client.ChatTtsTextAsync(
                _isUserQuerying, 
                false, 
                true, 
                "这是第一轮TTS的结束事件。"
            );
            
            // 等待10秒，与Python版本一致
            await Task.Delay(10000);
            
            await _client.ChatTtsTextAsync(
                _isUserQuerying, 
                true, 
                false, 
                "这是第二轮TTS的开始和中间包事件，这两个合而为一了。"
            );
            
            await _client.ChatTtsTextAsync(
                _isUserQuerying, 
                false, 
                true, 
                "这是第二轮TTS的结束事件。"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发ChatTtsText时发生错误");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isRunning = false;
            _isPlaying = false;
            
            _cancellationTokenSource?.Cancel();
            
            _audioDevice?.Dispose();
            _client?.Dispose();
            
            if (_outputStream != null)
            {
                _outputStream.Stop();
                _outputStream.Dispose();
            }
            
            _cancellationTokenSource?.Dispose();
            
            _logger.LogInformation("DoubaoRealTimeService资源已释放");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放资源时发生错误");
        }
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// 音频配置
/// </summary>
public class AudioConfig
{
    public string Format { get; set; } = "pcm";
    public int BitSize { get; set; }
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public int Chunk { get; set; }
}

/// <summary>
/// 音频设备管理器
/// </summary>
public class AudioDeviceManager : IDisposable
{
    private readonly AudioConfig _inputConfig;
    private readonly AudioConfig _outputConfig;
    private WaveInEvent? _inputStream;
    private WaveOutEvent? _outputStream;
    private bool _isRecording;
    private CancellationTokenSource? _recordingCts;

    public AudioDeviceManager(AudioConfig inputConfig, AudioConfig outputConfig)
    {
        _inputConfig = inputConfig;
        _outputConfig = outputConfig;
    }

    public async Task StartRecordingAsync(Func<byte[], Task> onAudioData, CancellationToken cancellationToken)
    {
        if (_isRecording) return;
        
        _recordingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _inputStream = new WaveInEvent
        {
            WaveFormat = new WaveFormat(_inputConfig.SampleRate, _inputConfig.Channels),
            BufferMilliseconds = 100
        };
        
        _inputStream.DataAvailable += async (sender, e) =>
        {
            if (_recordingCts.Token.IsCancellationRequested) return;
            
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);
            
            try
            {
                await onAudioData(audioData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理音频数据时发生错误: {ex.Message}");
            }
        };
        
        _inputStream.StartRecording();
        _isRecording = true;
        
        await Task.CompletedTask;
    }

    public async Task StopRecordingAsync()
    {
        if (!_isRecording) return;
        
        _isRecording = false;
        _recordingCts?.Cancel();
        
        if (_inputStream != null)
        {
            _inputStream.StopRecording();
            _inputStream.Dispose();
            _inputStream = null;
        }
        
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _recordingCts?.Cancel();
        _inputStream?.Dispose();
        _outputStream?.Dispose();
        _recordingCts?.Dispose();
    }
}

/// <summary>
/// 实时对话客户端 - 基于Python版本实现
/// </summary>
public class RealtimeDialogClient : IDisposable
{
    private readonly Dictionary<string, object> _config;
    private readonly string _sessionId;
    private ClientWebSocket? _webSocket;
    private readonly ILogger _logger;
    
    public string? LogId { get; private set; }

    public RealtimeDialogClient(Dictionary<string, object> config, string sessionId)
    {
        _config = config;
        _sessionId = sessionId;
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RealtimeDialogClient>.Instance;
    }

    public async Task ConnectAsync()
    {
        var baseUrl = _config["base_url"].ToString()!;
        var headers = (Dictionary<string, string>)_config["headers"];
        
        _logger.LogInformation("连接URL: {Url}, Headers: {Headers}", baseUrl, JsonConvert.SerializeObject(headers));
        
        _webSocket = new ClientWebSocket();
        
        foreach (var header in headers)
        {
            _webSocket.Options.SetRequestHeader(header.Key, header.Value);
        }
        
        await _webSocket.ConnectAsync(new Uri(baseUrl), CancellationToken.None);
        
        // 获取LogId
        if (_webSocket.State == WebSocketState.Open)
        {
            LogId = "generated-log-id"; // 实际应从响应头获取
            _logger.LogInformation("WebSocket连接成功，LogId: {LogId}", LogId);
        }
        
        // 发送StartConnection请求
        await SendStartConnectionAsync();
        
        // 发送StartSession请求
        await SendStartSessionAsync();
    }

    private async Task SendStartConnectionAsync()
    {
        var header = ProtocolHelper.GenerateHeader();
        var request = new List<byte>(header);
        
        // Event: 1
        request.AddRange(BitConverter.GetBytes(1).Reverse());
        
        // Payload
        var payloadBytes = Encoding.UTF8.GetBytes("{}");
        payloadBytes = ProtocolHelper.CompressGzip(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(payloadBytes.Length).Reverse());
        request.AddRange(payloadBytes);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        var response = await ReceiveAsync();
        var parsedResponse = ProtocolHelper.ParseResponse(response);
        _logger.LogInformation("StartConnection响应: {Response}", JsonConvert.SerializeObject(parsedResponse));
    }

    private async Task SendStartSessionAsync()
    {
        var requestParams = new
        {
            tts = new
            {
                audio_config = new
                {
                    channel = 1,
                    format = "pcm",
                    sample_rate = 24000
                }
            },
            dialog = new
            {
                bot_name = "豆包",
                system_role = "你使用活泼灵动的女声，性格开朗，热爱生活。",
                speaking_style = "你的说话风格简洁明了，语速适中，语调自然。",
                extra = new
                {
                    strict_audit = false,
                    audit_response = "支持客户自定义安全审核回复话术。"
                }
            }
        };
        
        var header = ProtocolHelper.GenerateHeader();
        var request = new List<byte>(header);
        
        // Event: 100
        request.AddRange(BitConverter.GetBytes(100).Reverse());
        
        // Session ID
        var sessionIdBytes = Encoding.UTF8.GetBytes(_sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        // Payload
        var payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestParams));
        payloadBytes = ProtocolHelper.CompressGzip(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(payloadBytes.Length).Reverse());
        request.AddRange(payloadBytes);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        var response = await ReceiveAsync();
        var parsedResponse = ProtocolHelper.ParseResponse(response);
        _logger.LogInformation("StartSession响应: {Response}", JsonConvert.SerializeObject(parsedResponse));
    }

    public async Task SayHelloAsync(string content)
    {
        var payload = new { content };
        
        var header = ProtocolHelper.GenerateHeader();
        var request = new List<byte>(header);
        
        // Event: 300
        request.AddRange(BitConverter.GetBytes(300).Reverse());
        
        // Session ID
        var sessionIdBytes = Encoding.UTF8.GetBytes(_sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        // Payload
        var payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
        payloadBytes = ProtocolHelper.CompressGzip(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(payloadBytes.Length).Reverse());
        request.AddRange(payloadBytes);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task ChatTtsTextAsync(bool isUserQuerying, bool start, bool end, string content)
    {
        if (isUserQuerying) return;
        
        var payload = new { start, end, content };
        
        _logger.LogInformation("ChatTTSTextRequest payload: {Payload}", JsonConvert.SerializeObject(payload));
        
        var header = ProtocolHelper.GenerateHeader();
        var request = new List<byte>(header);
        
        // Event: 500
        request.AddRange(BitConverter.GetBytes(500).Reverse());
        
        // Session ID
        var sessionIdBytes = Encoding.UTF8.GetBytes(_sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        // Payload
        var payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
        payloadBytes = ProtocolHelper.CompressGzip(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(payloadBytes.Length).Reverse());
        request.AddRange(payloadBytes);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task SendAudioDataAsync(byte[] audioData)
    {
        var header = ProtocolHelper.GenerateHeader(
            messageType: ProtocolHelper.CLIENT_AUDIO_ONLY_REQUEST,
            serialMethod: ProtocolHelper.NO_SERIALIZATION);
        
        var request = new List<byte>(header);
        
        // Event: 200
        request.AddRange(BitConverter.GetBytes(200).Reverse());
        
        // Session ID
        var sessionIdBytes = Encoding.UTF8.GetBytes(_sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        // Payload (compressed audio)
        var compressedAudio = ProtocolHelper.CompressGzip(audioData);
        request.AddRange(BitConverter.GetBytes(compressedAudio.Length).Reverse());
        request.AddRange(compressedAudio);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task<Dictionary<string, object>> ReceiveServerResponseAsync()
    {
        try
        {
            var response = await ReceiveAsync();
            return ProtocolHelper.ParseResponse(response);
        }
        catch (Exception ex)
        {
            throw new Exception($"接收消息失败: {ex.Message}", ex);
        }
    }

    public async Task FinishSessionAsync()
    {
        var header = ProtocolHelper.GenerateHeader();
        var request = new List<byte>(header);
        
        // Event: 102
        request.AddRange(BitConverter.GetBytes(102).Reverse());
        
        // Session ID
        var sessionIdBytes = Encoding.UTF8.GetBytes(_sessionId);
        request.AddRange(BitConverter.GetBytes(sessionIdBytes.Length).Reverse());
        request.AddRange(sessionIdBytes);
        
        // Payload
        var payloadBytes = Encoding.UTF8.GetBytes("{}");
        payloadBytes = ProtocolHelper.CompressGzip(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(payloadBytes.Length).Reverse());
        request.AddRange(payloadBytes);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task FinishConnectionAsync()
    {
        var header = ProtocolHelper.GenerateHeader();
        var request = new List<byte>(header);
        
        // Event: 2
        request.AddRange(BitConverter.GetBytes(2).Reverse());
        
        // Payload
        var payloadBytes = Encoding.UTF8.GetBytes("{}");
        payloadBytes = ProtocolHelper.CompressGzip(payloadBytes);
        
        request.AddRange(BitConverter.GetBytes(payloadBytes.Length).Reverse());
        request.AddRange(payloadBytes);
        
        await _webSocket!.SendAsync(request.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        var response = await ReceiveAsync();
        var parsedResponse = ProtocolHelper.ParseResponse(response);
        _logger.LogInformation("FinishConnection响应: {Response}", JsonConvert.SerializeObject(parsedResponse));
    }

    public async Task CloseAsync()
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            _logger.LogInformation("关闭WebSocket连接...");
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常关闭", CancellationToken.None);
        }
    }

    private async Task<byte[]> ReceiveAsync()
    {
        var buffer = new byte[8192];
        var result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        
        var responseData = new byte[result.Count];
        Array.Copy(buffer, responseData, result.Count);
        
        return responseData;
    }

    public void Dispose()
    {
        _webSocket?.Dispose();
    }
}

/// <summary>
/// 协议助手类 - 基于Python版本实现
/// </summary>
public static class ProtocolHelper
{
    // 协议常量
    public const byte PROTOCOL_VERSION = 0b0001;
    public const byte DEFAULT_HEADER_SIZE = 0b0001;
    public const byte CLIENT_FULL_REQUEST = 0b0001;
    public const byte CLIENT_AUDIO_ONLY_REQUEST = 0b0010;
    public const byte SERVER_FULL_RESPONSE = 0b1001;
    public const byte SERVER_ACK = 0b1011;
    public const byte SERVER_ERROR_RESPONSE = 0b1111;
    public const byte MSG_WITH_EVENT = 0b0100;
    public const byte JSON_SERIALIZATION = 0b0001;
    public const byte GZIP_COMPRESSION = 0b0001;
    public const byte NO_COMPRESSION = 0b0000;
    public const byte NO_SERIALIZATION = 0b0000;

    public static byte[] GenerateHeader(
        byte version = PROTOCOL_VERSION,
        byte messageType = CLIENT_FULL_REQUEST,
        byte messageTypeSpecificFlags = MSG_WITH_EVENT,
        byte serialMethod = JSON_SERIALIZATION,
        byte compressionType = GZIP_COMPRESSION,
        byte reservedData = 0x00,
        byte[] extensionHeader = null)
    {
        extensionHeader ??= Array.Empty<byte>();
        
        var header = new List<byte>();
        var headerSize = (byte)(extensionHeader.Length / 4 + 1);
        
        header.Add((byte)((version << 4) | headerSize));
        header.Add((byte)((messageType << 4) | messageTypeSpecificFlags));
        header.Add((byte)((serialMethod << 4) | compressionType));
        header.Add(reservedData);
        header.AddRange(extensionHeader);
        
        return header.ToArray();
    }

    public static Dictionary<string, object> ParseResponse(byte[] response)
    {
        if (response == null || response.Length == 0)
            return new Dictionary<string, object>();
        
        var protocolVersion = (byte)(response[0] >> 4);
        var headerSize = (byte)(response[0] & 0x0f);
        var messageType = (byte)(response[1] >> 4);
        var messageTypeSpecificFlags = (byte)(response[1] & 0x0f);
        var serializationMethod = (byte)(response[2] >> 4);
        var messageCompression = (byte)(response[2] & 0x0f);
        var reserved = response[3];
        
        var headerExtensions = new byte[headerSize * 4 - 4];
        if (headerExtensions.Length > 0)
        {
            Array.Copy(response, 4, headerExtensions, 0, headerExtensions.Length);
        }
        
        var payload = new byte[response.Length - headerSize * 4];
        Array.Copy(response, headerSize * 4, payload, 0, payload.Length);
        
        var result = new Dictionary<string, object>();
        object? payloadMsg = null;
        var payloadSize = 0;
        var start = 0;
        
        if (messageType == SERVER_FULL_RESPONSE || messageType == SERVER_ACK)
        {
            result["message_type"] = messageType == SERVER_ACK ? "SERVER_ACK" : "SERVER_FULL_RESPONSE";
            
            if ((messageTypeSpecificFlags & 0b0010) > 0) // NEG_SEQUENCE
            {
                result["seq"] = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
                start += 4;
            }
            
            if ((messageTypeSpecificFlags & MSG_WITH_EVENT) > 0)
            {
                result["event"] = BitConverter.ToInt32(payload.Skip(start).Take(4).Reverse().ToArray(), 0);
                start += 4;
            }
            
            payload = payload.Skip(start).ToArray();
            
            var sessionIdSize = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
            var sessionId = Encoding.UTF8.GetString(payload, 4, sessionIdSize);
            result["session_id"] = sessionId;
            
            payload = payload.Skip(4 + sessionIdSize).ToArray();
            payloadSize = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
            payloadMsg = payload.Skip(4).ToArray();
        }
        else if (messageType == SERVER_ERROR_RESPONSE)
        {
            result["message_type"] = "SERVER_ERROR";
            var code = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0);
            result["code"] = code;
            payloadSize = BitConverter.ToInt32(payload.Skip(4).Take(4).Reverse().ToArray(), 0);
            payloadMsg = payload.Skip(8).ToArray();
        }
        
        if (payloadMsg != null)
        {
            if (messageCompression == GZIP_COMPRESSION && payloadMsg is byte[] compressedData)
            {
                payloadMsg = DecompressGzip(compressedData);
            }
            
            if (serializationMethod == JSON_SERIALIZATION && payloadMsg is byte[] jsonData)
            {
                var jsonString = Encoding.UTF8.GetString(jsonData);
                payloadMsg = JsonConvert.DeserializeObject(jsonString);
            }
            else if (serializationMethod != NO_SERIALIZATION && payloadMsg is byte[] textData)
            {
                payloadMsg = Encoding.UTF8.GetString(textData);
            }
            
            result["payload_msg"] = payloadMsg;
            result["payload_size"] = payloadSize;
        }
        
        return result;
    }

    public static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public static byte[] DecompressGzip(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}

#endregion