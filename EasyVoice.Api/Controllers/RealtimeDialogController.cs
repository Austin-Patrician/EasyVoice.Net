using EasyVoice.RealtimeDialog.Models;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EasyVoice.Api.Controllers;

/// <summary>
/// 实时语音对话控制器
/// 提供豆包实时语音对话的HTTP API接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RealtimeDialogController : ControllerBase
{
    private readonly RealtimeDialogService _dialogService;
    private readonly AudioService _audioService;
    private readonly ILogger<RealtimeDialogController> _logger;
    
    public RealtimeDialogController(
        RealtimeDialogService dialogService,
        AudioService audioService,
        ILogger<RealtimeDialogController> logger)
    {
        _dialogService = dialogService;
        _audioService = audioService;
        _logger = logger;
    }
    
    /// <summary>
    /// 创建新的对话会话
    /// </summary>
    /// <param name="request">会话配置</param>
    /// <returns></returns>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var sessionId = await _dialogService.StartSessionAsync(request.Config);
            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest(new { error = "Failed to create session" });
            }
            
            return Ok(new { sessionId, status = "created" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 获取会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        try
        {
            var session = await _dialogService.GetSessionInfoAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }
            
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话信息失败: {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> EndSession(string sessionId)
    {
        try
        {
            var success = await _dialogService.EndSessionAsync(sessionId);
            if (!success)
            {
                return BadRequest(new { error = "Failed to end session" });
            }
            
            return Ok(new { status = "ended" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话失败: {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">音频数据请求</param>
    /// <returns></returns>
    [HttpPost("sessions/{sessionId}/audio")]
    public async Task<IActionResult> SendAudio(string sessionId, [FromBody] SendAudioRequest request)
    {
        try
        {
            if (request.AudioData == null || request.AudioData.Length == 0)
            {
                return BadRequest(new { error = "Audio data is required" });
            }
            
            var success = await _dialogService.SendAudioAsync(sessionId, request.AudioData, request.IsLast);
            if (!success)
            {
                return BadRequest(new { error = "Failed to send audio" });
            }
            
            return Ok(new { status = "sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频失败: {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 发送ChatTTSText请求
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">ChatTTSText请求</param>
    /// <returns></returns>
    [HttpPost("sessions/{sessionId}/chat-tts")]
    public async Task<IActionResult> SendChatTtsText(string sessionId, [FromBody] ChatTtsTextRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text is required" });
            }
            
            var success = await _dialogService.SendChatTtsTextAsync(request.Text, request.VoiceId, request.Speed, request.Emotion);
            if (!success)
            {
                return BadRequest(new { error = "Failed to send ChatTTSText" });
            }
            
            return Ok(new { status = "sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送ChatTTSText失败: {SessionId}", sessionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 获取音频设备列表
    /// </summary>
    /// <returns></returns>
    [HttpGet("audio/devices")]
    public IActionResult GetAudioDevices()
    {
        try
        {
            var inputDevices = _audioService.GetInputDevices();
            var outputDevices = _audioService.GetOutputDevices();
            
            return Ok(new 
            {
                inputDevices,
                outputDevices,
                defaultInput = _audioService.GetDefaultInputDevice(),
                defaultOutput = _audioService.GetDefaultOutputDevice()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取音频设备列表失败");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 设置音频配置
    /// </summary>
    /// <param name="config">音频配置</param>
    /// <returns></returns>
    [HttpPost("audio/config")]
    public IActionResult SetAudioConfig([FromBody] AudioConfig config)
    {
        try
        {
            var success = _audioService.SetAudioConfig(config);
            if (!success)
            {
                return BadRequest(new { error = "Failed to set audio config" });
            }
            
            return Ok(new { status = "configured" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置音频配置失败");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 开始录制音频
    /// </summary>
    /// <param name="request">录制请求</param>
    /// <returns></returns>
    [HttpPost("audio/recording/start")]
    public async Task<IActionResult> StartRecording([FromBody] StartRecordingRequest? request = null)
    {
        try
        {
            var success = await _audioService.StartRecordingAsync(request?.DeviceId);
            if (!success)
            {
                return BadRequest(new { error = "Failed to start recording" });
            }
            
            return Ok(new { status = "recording" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始录制失败");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 停止录制音频
    /// </summary>
    /// <returns></returns>
    [HttpPost("audio/recording/stop")]
    public async Task<IActionResult> StopRecording()
    {
        try
        {
            var success = await _audioService.StopRecordingAsync();
            if (!success)
            {
                return BadRequest(new { error = "Failed to stop recording" });
            }
            
            return Ok(new { status = "stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止录制失败");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
    
    /// <summary>
    /// 获取录制状态
    /// </summary>
    /// <returns></returns>
    [HttpGet("audio/recording/status")]
    public IActionResult GetRecordingStatus()
    {
        try
        {
            return Ok(new 
            {
                isRecording = _audioService.IsRecording,
                isPlaying = _audioService.IsPlaying,
                currentConfig = _audioService.GetCurrentConfig()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取录制状态失败");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

/// <summary>
/// 创建会话请求
/// </summary>
public class CreateSessionRequest
{
    [Required]
    public SessionConfig Config { get; set; } = new();
}

/// <summary>
/// 发送音频请求
/// </summary>
public class SendAudioRequest
{
    [Required]
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    
    public bool IsLast { get; set; } = false;
}

/// <summary>
/// ChatTTSText请求
/// </summary>
public class ChatTtsTextRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
    
    public string? VoiceId { get; set; }
    
    public float Speed { get; set; } = 1.0f;
    
    public string? Emotion { get; set; }
}

/// <summary>
/// 开始录制请求
/// </summary>
public class StartRecordingRequest
{
    public string? DeviceId { get; set; }
}