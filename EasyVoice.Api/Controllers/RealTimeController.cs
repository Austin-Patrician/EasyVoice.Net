using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace EasyVoice.Api.Controllers;

/// <summary>
/// 实时语音对话控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RealTimeController : ControllerBase
{
    private readonly IRealTimeService _realTimeService;
    private readonly ILogger<RealTimeController> _logger;
    private readonly IMemoryCache _sessionCache;
    private static readonly ConcurrentDictionary<string, IRealTimeService> ActiveSessions = new();
    private string? _currentSessionId;
    
    public RealTimeController(
        IRealTimeService realTimeService, 
        ILogger<RealTimeController> logger,
        IMemoryCache sessionCache)
    {
        _realTimeService = realTimeService;
        _logger = logger;
        _sessionCache = sessionCache;
    }
    
    
    /// <summary>
    /// 完整的实时对话流程示例
    /// </summary>
    /// <param name="appId">豆包应用ID</param>
    /// <param name="accessToken">豆包访问令牌</param>
    /// <returns>是否成功</returns>
    [HttpPost("run")]
    public async Task<bool> RunCompleteDialogExampleAsync(string appId, string accessToken)
    {
        try
        {
            _logger.LogInformation("开始实时语音对话示例");

            // 1. 配置连接参数
            var config = new RealTimeConnectionConfig
            {
                AppId = "7482136989",
                AccessToken = "4akGrrTRlikgCCxBVSi0f3gXQ2uGR8bt",
                WebSocketUrl = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue",
                ConnectionTimeoutMs = 30000,
                AudioBufferSeconds = 100
            };

            // 2. 连接到服务
            _logger.LogInformation("正在连接到豆包实时语音服务...");
            var connected = await _realTimeService.ConnectAsync(config);
            if (!connected)
            {
                _logger.LogError("连接失败");
                return false;
            }
            _logger.LogInformation("连接成功！");

            // 3. 开始会话
            _currentSessionId = Guid.NewGuid().ToString();
            var sessionPayload = new StartSessionPayload
            {
                Tts = new TtsConfig
                {
                    AudioConfig = new AudioConfig
                    {
                        Channel = 1,
                        Format = "pcm",
                        SampleRate = 24000
                    }
                },
                Dialog = new DialogConfig
                {
                    BotName = "豆包助手",
                    SystemRole = "你是一个友好的AI助手，使用活泼灵动的女声，性格开朗，热爱生活。",
                    SpeakingStyle = "你的说话风格简洁明了，语速适中，语调自然。"
                }
            };

            _logger.LogInformation("正在启动会话: {SessionId}", _currentSessionId);
            var sessionStarted = await _realTimeService.StartSessionAsync(_currentSessionId, sessionPayload);
            if (!sessionStarted)
            {
                _logger.LogError("启动会话失败");
                await _realTimeService.DisconnectAsync();
                return false;
            }
            _logger.LogInformation("会话启动成功！");

            // 4. 发送问候语
            var helloPayload = new SayHelloPayload
            {
                Content = "你好！我是豆包，很高兴为您服务！有什么可以帮助您的吗？"
            };

            _logger.LogInformation("发送问候语...");
            var helloSent = await _realTimeService.SayHelloAsync(_currentSessionId, helloPayload);
            if (!helloSent)
            {
                _logger.LogWarning("发送问候语失败，但会话仍可继续");
            }
            else
            {
                _logger.LogInformation("问候语发送成功！");
            }

            // 5. 开始音频录制和播放
            _logger.LogInformation("开始音频录制...");
            await _realTimeService.StartAudioRecordingAsync(_currentSessionId);

            _logger.LogInformation("开始音频播放...");
            await _realTimeService.StartAudioPlaybackAsync();

            // 6. 模拟运行一段时间（实际使用中这里会是用户交互）
            _logger.LogInformation("实时对话已启动，模拟运行30秒...");
            await Task.Delay(30000);

            // 7. 停止音频处理
            _logger.LogInformation("停止音频录制和播放...");
            await _realTimeService.StopAudioRecordingAsync();
            await _realTimeService.StopAudioPlaybackAsync();

            // 8. 结束会话
            _logger.LogInformation("结束会话...");
            await _realTimeService.FinishSessionAsync(_currentSessionId);

            // 9. 断开连接
            _logger.LogInformation("断开连接...");
            await _realTimeService.DisconnectAsync();

            _logger.LogInformation("实时语音对话示例完成！");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "实时语音对话示例执行失败");
            return false;
        }
    }
    
    
    /// <summary>
    /// 创建实时对话会话
    /// </summary>
    /// <param name="request">连接配置</param>
    /// <returns>会话信息</returns>
    [HttpPost("session/create")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();
            var config = new RealTimeConnectionConfig
            {
                AppId = "7482136989",
                AccessToken = "4akGrrTRlikgCCxBVSi0f3gXQ2uGR8bt",
                WebSocketUrl = request.WebSocketUrl ?? "wss://openspeech.bytedance.com/api/v3/realtime/dialogue",
                ConnectionTimeoutMs = request.ConnectionTimeoutMs ?? 30000,
                AudioBufferSeconds = request.AudioBufferSeconds ?? 100
            };

            // 缓存会话配置
            _sessionCache.Set(sessionId, config, TimeSpan.FromHours(1));
            
            // 创建会话信息
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                State = RealTimeDialogState.Disconnected
            };
            _sessionCache.Set($"session_info_{sessionId}", sessionInfo, TimeSpan.FromHours(1));
            
            _logger.LogInformation("会话创建成功: {SessionId}", sessionId);
            
            return Ok(new CreateSessionResponse
            {
                SessionId = sessionId,
                Status = "created",
                Message = "会话创建成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            return BadRequest(new { error = "创建会话失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 启动会话连接
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">会话配置</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/start")]
    public async Task<IActionResult> StartSession(string sessionId, [FromBody] StartSessionRequest request)
    {
        try
        {
            if (!_sessionCache.TryGetValue(sessionId, out RealTimeConnectionConfig? config))
            {
                return NotFound(new { error = "会话不存在" });
            }

            // 创建新的服务实例
            var serviceProvider = HttpContext.RequestServices;
            var realTimeService = serviceProvider.GetRequiredService<IRealTimeService>();

            // 设置事件处理
            SetupEventHandlers(realTimeService, sessionId);

            // 连接到服务
            var connected = await realTimeService.ConnectAsync(config);
            if (!connected)
            {
                return BadRequest(new { error = "连接失败" });
            }

            var tts = new AudioConfig()
            {
                Channel = 1,
                Format = "pcm",
                SampleRate = 24000
            };
            if (request.AudioConfig is not null)
            {
                tts.Channel = request.AudioConfig.Channel ?? 1;
                tts.Format = request.AudioConfig.Format ?? "pcm";
                tts.SampleRate = request.AudioConfig.SampleRate ?? 24000;
            }
            
            // 启动会话
            var payload = new StartSessionPayload
            {
                Tts = new TtsConfig
                {
                    AudioConfig = tts
                },
                Dialog = new DialogConfig
                {
                    BotName = request.BotName ?? "豆包",
                    SystemRole = request.SystemRole ?? "你使用活泼灵动的女声，性格开朗，热爱生活。",
                    SpeakingStyle = request.SpeakingStyle ?? "你的说话风格简洁明了，语速适中，语调自然。"
                }
            };

            var started = await realTimeService.StartSessionAsync(sessionId, payload);
            if (!started)
            {
                return BadRequest(new { error = "启动会话失败" });
            }

            // 保存会话服务实例
            ActiveSessions[sessionId] = realTimeService;
            
            // 更新会话状态
            if (_sessionCache.TryGetValue($"session_info_{sessionId}", out SessionInfo? sessionInfo))
            {
                sessionInfo.State = RealTimeDialogState.Connected;
                sessionInfo.LastActiveAt = DateTime.UtcNow;
                _sessionCache.Set($"session_info_{sessionId}", sessionInfo, TimeSpan.FromHours(1));
            }
            
            _logger.LogInformation("会话启动成功: {SessionId}", sessionId);
            
            return Ok(new { status = "started", message = "会话启动成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动会话失败: {SessionId}", sessionId);
            return BadRequest(new { error = "启动会话失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 发送问候语
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">问候语内容</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/hello")]
    public async Task<IActionResult> SayHello(string sessionId, [FromBody] SayHelloRequest request)
    {
        try
        {
            if (!ActiveSessions.TryGetValue(sessionId, out var service))
            {
                return NotFound(new { error = "会话不存在" });
            }

            var payload = new SayHelloPayload
            {
                Content = request.Content
            };

            var sent = await service.SayHelloAsync(sessionId, payload);
            if (!sent)
            {
                return BadRequest(new { error = "发送问候语失败" });
            }

            _logger.LogInformation("发送问候语: {SessionId}, 内容: {Content}", sessionId, request.Content);

            return Ok(new
            {
                sessionId,
                status = "sent",
                message = "问候语发送成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送问候语时发生错误: {SessionId}", sessionId);
            return StatusCode(500, new { error = "发送问候语失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 开始音频录制
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/audio/start-recording")]
    public async Task<IActionResult> StartRecording(string sessionId)
    {
        try
        {
            if (!ActiveSessions.TryGetValue(sessionId, out var service))
            {
                return NotFound(new { error = "会话不存在" });
            }

            await service.StartAudioRecordingAsync(sessionId);
            _logger.LogInformation("开始音频录制: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                status = "recording",
                message = "音频录制已开始"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始音频录制时发生错误: {SessionId}", sessionId);
            return StatusCode(500, new { error = "开始录制失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 停止音频录制
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/audio/stop-recording")]
    public async Task<IActionResult> StopRecording(string sessionId)
    {
        try
        {
            if (!ActiveSessions.TryGetValue(sessionId, out var service))
            {
                return NotFound(new { error = "会话不存在" });
            }

            await service.StopAudioRecordingAsync();
            _logger.LogInformation("停止音频录制: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                status = "stopped",
                message = "音频录制已停止"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频录制时发生错误: {SessionId}", sessionId);
            return StatusCode(500, new { error = "停止录制失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 开始音频播放
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/audio/start-playback")]
    public async Task<IActionResult> StartPlayback(string sessionId)
    {
        try
        {
            if (!ActiveSessions.TryGetValue(sessionId, out var service))
            {
                return NotFound(new { error = "会话不存在" });
            }

            await service.StartAudioPlaybackAsync();
            _logger.LogInformation("开始音频播放: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                status = "playing",
                message = "音频播放已开始"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始音频播放时发生错误: {SessionId}", sessionId);
            return StatusCode(500, new { error = "开始播放失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 停止音频播放
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/audio/stop-playback")]
    public async Task<IActionResult> StopPlayback(string sessionId)
    {
        try
        {
            if (!ActiveSessions.TryGetValue(sessionId, out var service))
            {
                return NotFound(new { error = "会话不存在" });
            }

            await service.StopAudioPlaybackAsync();
            _logger.LogInformation("停止音频播放: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                status = "stopped",
                message = "音频播放已停止"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止音频播放时发生错误: {SessionId}", sessionId);
            return StatusCode(500, new { error = "停止播放失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>操作结果</returns>
    [HttpPost("session/{sessionId}/finish")]
    public async Task<IActionResult> FinishSession(string sessionId)
    {
        try
        {
            // 检查会话是否存在
            if (!_sessionCache.TryGetValue($"session_info_{sessionId}", out SessionInfo? sessionInfo))
            {
                return NotFound(new { error = "会话不存在" });
            }

            // 如果有活跃的服务实例，先结束会话
            if (ActiveSessions.TryGetValue(sessionId, out var service))
            {
                try
                {
                    await service.FinishSessionAsync(sessionId);
                    await service.DisconnectAsync();
                    service.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "结束服务实例时发生错误: {SessionId}", sessionId);
                }
                finally
                {
                    ActiveSessions.TryRemove(sessionId, out _);
                }
            }

            // 清理缓存
            _sessionCache.Remove(sessionId);
            _sessionCache.Remove($"session_info_{sessionId}");

            _logger.LogInformation("会话结束成功: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                status = "finished",
                message = "会话已结束"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束对话会话时发生错误: {SessionId}", sessionId);
            return StatusCode(500, new { error = "结束会话失败", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取会话状态
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>会话状态</returns>
    [HttpGet("session/{sessionId}/status")]
    public IActionResult GetSessionStatus(string sessionId)
    {
        try
        {
            if (!_sessionCache.TryGetValue($"session_info_{sessionId}", out SessionInfo? sessionInfo))
            {
                return NotFound(new { error = "会话不存在" });
            }

            var status = new SessionStatusResponse
            {
                SessionId = sessionId,
                State = sessionInfo.State.ToString(),
                CreatedAt = sessionInfo.CreatedAt,
                LastActiveAt = sessionInfo.LastActiveAt,
                IsConnected = sessionInfo.State == RealTimeDialogState.Connected || sessionInfo.State == RealTimeDialogState.InSession
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话状态失败: {SessionId}", sessionId);
            return BadRequest(new { error = "获取会话状态失败", details = ex.Message });
        }
    }

    /// <summary>
    /// WebSocket连接处理实时音频流
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>WebSocket连接</returns>
    [HttpGet("session/{sessionId}/websocket")]
    public async Task<IActionResult> WebSocketConnection(string sessionId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            return BadRequest("需要WebSocket连接");
        }

        if (!ActiveSessions.TryGetValue(sessionId, out var service))
        {
            return NotFound(new { error = "会话不存在" });
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocketConnection(webSocket, service, sessionId);

        return new EmptyResult();
    }

    /// <summary>
    /// 设置事件处理器
    /// </summary>
    private void SetupEventHandlers(IRealTimeService service, string sessionId)
    {
        service.ConnectionStateChanged += (sender, args) =>
        {
            _logger.LogInformation("会话 {SessionId} 连接状态变化: {State}", sessionId, args.NewState);
        };

        service.AudioDataReceived += (sender, args) =>
        {
            _logger.LogDebug("会话 {SessionId} 接收到音频数据: {Length} 字节", sessionId, args.AudioData.Length);
        };

        service.DialogEvent += (sender, args) =>
        {
            _logger.LogInformation("会话 {SessionId} 对话事件: {EventType}, 会话ID: {ArgSessionId}, 数据: {Data}", 
                sessionId, args.EventType, args.SessionId, args.Data);
        };

        service.ErrorOccurred += (sender, args) =>
        {
            _logger.LogError(args.Exception, "会话 {SessionId} 发生错误: {Message}", sessionId, args.ErrorMessage);
        };
    }

    /// <summary>
    /// 处理WebSocket连接
    /// </summary>
    private async Task HandleWebSocketConnection(WebSocket webSocket, IRealTimeService service, string sessionId)
    {
        var buffer = new byte[1024 * 4];
        var cancellationToken = HttpContext.RequestAborted;

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // 接收到音频数据，发送到服务
                    var audioData = new byte[result.Count];
                    Array.Copy(buffer, audioData, result.Count);
                    await service.SendAudioDataAsync(sessionId, audioData);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    // 接收到文本消息
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation("收到WebSocket文本消息: {Message}", message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "连接关闭", cancellationToken);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket连接处理时发生错误: {SessionId}", sessionId);
        }
    }
}

#region Request Models

/// <summary>
/// 创建会话请求
/// </summary>
public class CreateSessionRequest
{
    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// WebSocket URL（可选）
    /// </summary>
    public string? WebSocketUrl { get; set; }

    /// <summary>
    /// 连接超时时间（毫秒，可选）
    /// </summary>
    public int? ConnectionTimeoutMs { get; set; }

    /// <summary>
    /// 音频缓冲区大小（秒，可选）
    /// </summary>
    public int? AudioBufferSeconds { get; set; }
}

/// <summary>
/// 开始会话请求
/// </summary>
public class StartSessionRequest
{
    /// <summary>
    /// 机器人名称
    /// </summary>
    public string? BotName { get; set; }

    /// <summary>
    /// 系统角色设定
    /// </summary>
    public string? SystemRole { get; set; }

    /// <summary>
    /// 说话风格
    /// </summary>
    public string? SpeakingStyle { get; set; }

    /// <summary>
    /// 音频配置
    /// </summary>
    public AudioConfigRequest? AudioConfig { get; set; }
}

/// <summary>
/// 音频配置请求
/// </summary>
public class AudioConfigRequest
{
    /// <summary>
    /// 音频通道数
    /// </summary>
    public int? Channel { get; set; }

    /// <summary>
    /// 音频格式
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 采样率
    /// </summary>
    public int? SampleRate { get; set; }
}

/// <summary>
/// 问候语请求
/// </summary>
public class SayHelloRequest
{
    /// <summary>
    /// 问候内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

#endregion

#region Response Models

/// <summary>
/// 创建会话响应
/// </summary>
public class CreateSessionResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 会话信息
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 会话状态
    /// </summary>
    public RealTimeDialogState State { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后活跃时间
    /// </summary>
    public DateTime LastActiveAt { get; set; }
}

/// <summary>
/// 会话状态响应
/// </summary>
public class SessionStatusResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 状态
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后活跃时间
    /// </summary>
    public DateTime LastActiveAt { get; set; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected { get; set; }
}

/// <summary>
/// 实时对话状态枚举
/// </summary>
public enum RealTimeDialogState
{
    /// <summary>
    /// 已创建
    /// </summary>
    Created,

    /// <summary>
    /// 连接中
    /// </summary>
    Connecting,

    /// <summary>
    /// 已连接
    /// </summary>
    Connected,

    /// <summary>
    /// 会话中
    /// </summary>
    InSession,

    /// <summary>
    /// 断开连接中
    /// </summary>
    Disconnecting,

    /// <summary>
    /// 已断开
    /// </summary>
    Disconnected,

    /// <summary>
    /// 错误状态
    /// </summary>
    Error
}

#endregion