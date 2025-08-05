using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EasyVoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TtsController : ControllerBase
{
    private readonly ITtsService _ttsService;
    private readonly ILogger<TtsController> _logger;
    private readonly ILlmService _llmService;
    private readonly IRealTimeService _realTimeService;

    public TtsController(ITtsService ttsService, ILogger<TtsController> logger, ILlmService llmService, IRealTimeService realTimeService)
    {
        _ttsService = ttsService;
        _logger = logger;
        _llmService = llmService;
        _realTimeService = realTimeService;
    }

    
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] EdgeTtsRequest request)
    {
        _logger.LogInformation("Received TTS generation request.");

        try
        {
            var result = await _ttsService.GenerateTtsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during TTS generation.");
            return StatusCode(500, "An internal server error occurred.");
        }
    }
    
    [HttpPost("stream")]
    public async Task<IActionResult> Stream([FromBody] EdgeTtsRequest request)
    {
        _logger.LogInformation("Received TTS stream request.");

        try
        {
            var audioStream = await _ttsService.GenerateTtsStreamAsync(request);
            
            // Set appropriate headers for streaming audio
            Response.Headers["Content-Type"] = "audio/mpeg";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Access-Control-Expose-Headers"] = "Content-Type";
            
            // Generate filename based on voice and timestamp
            var fileName = $"tts_{request.Voice}_{DateTime.Now:yyyyMMddHHmmss}.mp3";
            
            return new FileStreamResult(audioStream, "audio/mpeg")
            {
                FileDownloadName = fileName,
                EnableRangeProcessing = true // Enable range requests for better streaming support
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during TTS streaming generation.");
            return StatusCode(500, "An internal server error occurred during streaming.");
        }
    }
    
    
    [HttpPost("oepani-tts")]
    public async Task<IActionResult> OpenaiTts([FromBody] TtsRequest request)
    {
        _logger.LogInformation("Received TTS stream request.");

        try
        {
            var response = await _llmService.GenerateWithOpenAiAsync(request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during TTS streaming generation.");
            return StatusCode(500, "An internal server error occurred during streaming.");
        }
    }
    
    
    [HttpPost("doubao-tts")]
    public async Task<IActionResult> DoubaoTts([FromBody] TtsRequest request)
    {
        _logger.LogInformation("Received TTS stream request.");

        try
        {
            var response = await _llmService.GenerateWithDoubaoAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during TTS streaming generation.");
            return StatusCode(500, "An internal server error occurred during streaming.");
        }
    }

    /// <summary>
    /// 豆包实时语音对话快速示例
    /// 演示如何使用DoubaoRealTimeService进行实时语音对话
    /// </summary>
    /// <param name="request">实时对话请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("realtime-dialog-demo")]
    public async Task<IActionResult> RealTimeDialogDemo([FromBody] RealTimeDialogDemoRequest request)
    {
        _logger.LogInformation("开始豆包实时语音对话演示");

        try
        {
            // 1. 配置连接参数
            var config = new RealTimeConnectionConfig
            {
                AppId = request.AppId,
                AccessToken = request.AccessToken,
                WebSocketUrl = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue",
                ConnectionTimeoutMs = 30000,
                AudioBufferSeconds = 100
            };

            // 2. 设置事件处理器
            _realTimeService.ConnectionStateChanged += (sender, args) =>
            {
                _logger.LogInformation("连接状态变化: {State}", args.NewState);
            };

            _realTimeService.AudioDataReceived += (sender, args) =>
            {
                _logger.LogDebug("接收到音频数据: {Length} 字节", args.AudioData.Length);
            };

            _realTimeService.DialogEvent += (sender, args) =>
             {
                 _logger.LogInformation("对话事件: {EventType}, 会话ID: {SessionId}, 数据: {Data}", args.EventType, args.SessionId, args.Data);
             };

            _realTimeService.ErrorOccurred += (sender, args) =>
            {
                _logger.LogError(args.Exception, "发生错误: {Message}", args.ErrorMessage);
            };

            // 3. 连接到服务
            var connected = await _realTimeService.ConnectAsync(config);
            if (!connected)
            {
                return BadRequest(new { error = "连接失败" });
            }

            // 4. 开始会话
            var sessionId = Guid.NewGuid().ToString();
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
                    BotName = request.BotName ?? "豆包",
                    SystemRole = request.SystemRole ?? "你是一个友好的AI助手，使用活泼灵动的女声。",
                    SpeakingStyle = "你的说话风格简洁明了，语速适中，语调自然。"
                }
            };

            var sessionStarted = await _realTimeService.StartSessionAsync(sessionId, sessionPayload);
            if (!sessionStarted)
            {
                await _realTimeService.DisconnectAsync();
                return BadRequest(new { error = "启动会话失败" });
            }

            // 5. 发送问候语（可选）
            if (!string.IsNullOrEmpty(request.GreetingMessage))
            {
                var helloPayload = new SayHelloPayload
                {
                    Content = request.GreetingMessage
                };
                await _realTimeService.SayHelloAsync(sessionId, helloPayload);
            }

            _logger.LogInformation("实时语音对话会话创建成功: {SessionId}", sessionId);

            return Ok(new
            {
                sessionId,
                status = "connected",
                message = "实时语音对话会话创建成功",
                connectionState = _realTimeService.ConnectionState.ToString(),
                instructions = new
                {
                    next_steps = new[]
                    {
                        "使用 /api/realtime/session/{sessionId}/audio/start-recording 开始录音",
                        "使用 /api/realtime/session/{sessionId}/audio/start-playback 开始播放",
                        "使用 WebSocket 连接 /api/realtime/session/{sessionId}/websocket 进行实时音频流传输",
                        "使用 /api/realtime/session/{sessionId}/finish 结束会话"
                    },
                    audio_format = "PCM, 24kHz, 单声道",
                    websocket_url = $"/api/realtime/session/{sessionId}/websocket"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "实时语音对话演示时发生错误");
            return StatusCode(500, new { error = "创建实时对话失败", details = ex.Message });
        }
    }
}

/// <summary>
/// 实时语音对话演示请求
/// </summary>
public class RealTimeDialogDemoRequest
{
    /// <summary>
    /// 豆包应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 豆包访问令牌
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 机器人名称（可选）
    /// </summary>
    public string? BotName { get; set; }

    /// <summary>
    /// 系统角色设定（可选）
    /// </summary>
    public string? SystemRole { get; set; }

    /// <summary>
    /// 问候语（可选）
    /// </summary>
    public string? GreetingMessage { get; set; }
}
