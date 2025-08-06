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

    public TtsController(ITtsService ttsService, ILogger<TtsController> logger, ILlmService llmService)
    {
        _ttsService = ttsService;
        _logger = logger;
        _llmService = llmService;
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
