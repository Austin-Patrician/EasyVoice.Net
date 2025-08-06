using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using EasyVoice.RealtimeDialog.Models.Protocol;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// WebSocket客户端管理器
/// 负责与豆包实时语音API建立和维护WebSocket连接
/// </summary>
public class WebSocketClientManager : IDisposable
{
    private readonly ILogger<WebSocketClientManager> _logger;
    private readonly DoubaoProtocolHandler _protocolHandler;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    
    // 豆包API配置
    private const string BASE_URL = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue";
    private const string RESOURCE_ID = "volc.speech.dialog";
    private const string APP_KEY = "PlgvMymc7f3tQnJ6";
    
    public string? LogId { get; private set; }
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    
    // 事件
    public event Func<DoubaoResponse, Task>? OnMessageReceived;
    public event Func<Exception, Task>? OnError;
    public event Func<Task>? OnDisconnected;
    
    public WebSocketClientManager(
        ILogger<WebSocketClientManager> logger,
        DoubaoProtocolHandler protocolHandler)
    {
        _logger = logger;
        _protocolHandler = protocolHandler;
    }
    
    /// <summary>
    /// 连接到豆包实时语音API
    /// </summary>
    public async Task<bool> ConnectAsync(string appId, string accessKey, string? connectId = null)
    {
        try
        {
            if (_webSocket != null)
            {
                await DisconnectAsync();
            }
            
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 设置请求头
            _webSocket.Options.SetRequestHeader("X-Api-App-ID", appId);
            _webSocket.Options.SetRequestHeader("X-Api-Access-Key", accessKey);
            _webSocket.Options.SetRequestHeader("X-Api-Resource-Id", RESOURCE_ID);
            _webSocket.Options.SetRequestHeader("X-Api-App-Key", APP_KEY);
            
            if (!string.IsNullOrEmpty(connectId))
            {
                _webSocket.Options.SetRequestHeader("X-Api-Connect-Id", connectId);
            }
            
            // 建立连接
            await _webSocket.ConnectAsync(new Uri(BASE_URL), _cancellationTokenSource.Token);
            
            // 获取LogId
            if (_webSocket.HttpResponseHeaders != null)
            {
                LogId = _webSocket.HttpResponseHeaders.FirstOrDefault(h => 
                    h.Key.Equals("X-Tt-Logid", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
            }
            
            _logger.LogInformation("WebSocket连接成功，LogId: {LogId}", LogId);
            
            // 发送StartConnection请求
            var startConnectionRequest = _protocolHandler.CreateStartConnectionRequest();
            await SendBinaryAsync(startConnectionRequest);
            
            // 接收StartConnection响应
            var response = await ReceiveAsync();
            _logger.LogInformation("StartConnection响应: {Response}", 
                System.Text.Json.JsonSerializer.Serialize(response));
            
            // 启动接收循环
            _ = Task.Run(ReceiveLoop, _cancellationTokenSource.Token);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket连接失败");
            await OnError?.Invoke(ex)!;
            return false;
        }
    }
    
    /// <summary>
    /// 开始会话
    /// </summary>
    public async Task<bool> StartSessionAsync(string sessionId, object sessionConfig)
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogWarning("WebSocket未连接，无法开始会话");
                return false;
            }
            
            var startSessionRequest = _protocolHandler.CreateStartSessionRequest(sessionId, sessionConfig);
            await SendBinaryAsync(startSessionRequest);
            
            _logger.LogInformation("StartSession请求已发送，SessionId: {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始会话失败");
            await OnError?.Invoke(ex)!;
            return false;
        }
    }
    
    /// <summary>
    /// 发送音频数据（TaskRequest）
    /// </summary>
    public async Task<bool> SendAudioAsync(string sessionId, byte[] audioData)
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogWarning("WebSocket未连接，无法发送音频");
                return false;
            }
            
            var taskRequest = _protocolHandler.CreateTaskRequest(sessionId, audioData);
            await SendBinaryAsync(taskRequest);
            
            _logger.LogDebug("音频数据已发送，大小: {Size} 字节", audioData.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频失败");
            await OnError?.Invoke(ex)!;
            return false;
        }
    }
    
    /// <summary>
    /// 发送ChatTTSText请求（事件500）
    /// </summary>
    public async Task<bool> SendChatTtsTextAsync(string sessionId, string content, bool start, bool end)
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogWarning("WebSocket未连接，无法发送ChatTTSText");
                return false;
            }
            
            var chatTtsRequest = _protocolHandler.CreateChatTtsTextRequest(sessionId, content, start, end);
            await SendBinaryAsync(chatTtsRequest);
            
            _logger.LogInformation("ChatTTSText请求已发送: {Content}, Start: {Start}, End: {End}", 
                content, start, end);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送ChatTTSText失败");
            await OnError?.Invoke(ex)!;
            return false;
        }
    }
    
    /// <summary>
    /// 发送Hello消息（事件300）
    /// </summary>
    public async Task<bool> SayHelloAsync()
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogWarning("WebSocket未连接，无法发送Hello消息");
                return false;
            }
            
            var helloRequest = _protocolHandler.CreateHelloRequest();
            await SendBinaryAsync(helloRequest);
            
            _logger.LogInformation("Hello消息已发送");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送Hello消息失败");
            await OnError?.Invoke(ex)!;
            return false;
        }
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    public async Task<bool> FinishSessionAsync(string sessionId)
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogWarning("WebSocket未连接，无法结束会话");
                return false;
            }
            
            var finishSessionRequest = _protocolHandler.CreateFinishSessionRequest(sessionId);
            await SendBinaryAsync(finishSessionRequest);
            
            _logger.LogInformation("FinishSession请求已发送，SessionId: {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话失败");
            await OnError?.Invoke(ex)!;
            return false;
        }
    }
    
    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                // 发送FinishConnection请求
                var finishConnectionRequest = _protocolHandler.CreateFinishConnectionRequest();
                await SendBinaryAsync(finishConnectionRequest);
                
                // 关闭WebSocket连接
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常关闭", 
                    CancellationToken.None);
            }
            
            _cancellationTokenSource?.Cancel();
            _logger.LogInformation("WebSocket连接已断开");
            await OnDisconnected?.Invoke()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开连接失败");
        }
    }
    
    /// <summary>
    /// 发送二进制数据
    /// </summary>
    private async Task SendBinaryAsync(byte[] data)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                _cancellationTokenSource?.Token ?? CancellationToken.None);
        }
    }
    
    /// <summary>
    /// 接收消息
    /// </summary>
    private async Task<DoubaoResponse> ReceiveAsync()
    {
        var buffer = new byte[8192];
        var result = await _webSocket!.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            _cancellationTokenSource?.Token ?? CancellationToken.None);
        
        var responseData = new byte[result.Count];
        Array.Copy(buffer, responseData, result.Count);
        
        return _protocolHandler.ParseResponse(responseData);
    }
    
    /// <summary>
    /// 接收循环
    /// </summary>
    private async Task ReceiveLoop()
    {
        try
        {
            while (_webSocket?.State == WebSocketState.Open && 
                   !(_cancellationTokenSource?.Token.IsCancellationRequested ?? true))
            {
                var response = await ReceiveAsync();
                await OnMessageReceived?.Invoke(response)!;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("接收循环已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接收循环异常");
            await OnError?.Invoke(ex)!;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _webSocket?.Dispose();
            _disposed = true;
        }
    }
}