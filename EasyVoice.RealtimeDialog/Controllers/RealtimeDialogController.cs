using EasyVoice.RealtimeDialog.Models.Session;
using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace EasyVoice.RealtimeDialog.Controllers;

/// <summary>
/// 实时对话控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RealtimeDialogController : ControllerBase
{
    private readonly ILogger<RealtimeDialogController> _logger;
    private readonly IRealtimeDialogService _dialogService;
    
    public RealtimeDialogController(
        ILogger<RealtimeDialogController> logger,
        IRealtimeDialogService dialogService)
    {
        _logger = logger;
        _dialogService = dialogService;
    }
    
    /// <summary>
    /// 创建新的对话会话
    /// </summary>
    /// <param name="request">创建会话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息</returns>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(CreateSessionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("创建对话会话请求: {BotName}", request.BotName);
            
            var config = new SessionConfig
            {
                BotName = request.BotName,
                SystemRole = request.SystemRole ?? "你是一个智能助手，请用友好、专业的语气回答用户的问题。",
                AudioConfig = request.AudioConfig ?? new Models.Audio.AudioConfig(),
                VoiceConfig = request.VoiceConfig ?? new VoiceConfig(),
                UserConfig = new UserConfig
                {
                    UserId = GetCurrentUserId(),
                    UserName = GetCurrentUserName(),
                    Language = request.Language ?? "zh-CN",
                    Timezone = request.Timezone ?? "Asia/Shanghai"
                }
            };
            
            var sessionId = await _dialogService.CreateSessionAsync(config, cancellationToken);
            
            var response = new CreateSessionResponse
            {
                SessionId = sessionId,
                Status = "created",
                CreatedAt = DateTime.UtcNow,
                Config = config
            };
            
            _logger.LogInformation("会话创建成功: {SessionId}", sessionId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建会话失败");
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "SESSION_CREATE_FAILED",
                ErrorMessage = "创建会话失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 获取会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>会话信息</returns>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(SessionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            // 检查用户权限
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            var response = new SessionResponse
            {
                SessionId = session.SessionId,
                Status = session.Status.ToString().ToLower(),
                CreatedAt = session.CreatedAt,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                LastActivity = session.LastActivity,
                Config = session.Config,
                MessageCount = session.Messages.Count
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话失败: {SessionId}", sessionId);
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "SESSION_GET_FAILED",
                ErrorMessage = "获取会话失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 启动会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动结果</returns>
    [HttpPost("sessions/{sessionId}/start")]
    [ProducesResponseType(typeof(SessionActionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> StartSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            await _dialogService.StartSessionAsync(sessionId, cancellationToken);
            
            var response = new SessionActionResponse
            {
                SessionId = sessionId,
                Action = "start",
                Status = "success",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Message = "会话已启动"
            };
            
            _logger.LogInformation("会话启动成功: {SessionId}", sessionId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动会话失败: {SessionId}", sessionId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "SESSION_START_FAILED",
                ErrorMessage = "启动会话失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结束结果</returns>
    [HttpPost("sessions/{sessionId}/end")]
    [ProducesResponseType(typeof(SessionActionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> EndSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            await _dialogService.EndSessionAsync(sessionId, cancellationToken);
            
            var response = new SessionActionResponse
            {
                SessionId = sessionId,
                Action = "end",
                Status = "success",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Message = "会话已结束"
            };
            
            _logger.LogInformation("会话结束成功: {SessionId}", sessionId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话失败: {SessionId}", sessionId);
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "SESSION_END_FAILED",
                ErrorMessage = "结束会话失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 删除会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(typeof(SessionActionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> DeleteSession(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            await _dialogService.DeleteSessionAsync(sessionId, cancellationToken);
            
            var response = new SessionActionResponse
            {
                SessionId = sessionId,
                Action = "delete",
                Status = "success",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Message = "会话已删除"
            };
            
            _logger.LogInformation("会话删除成功: {SessionId}", sessionId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话失败: {SessionId}", sessionId);
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "SESSION_DELETE_FAILED",
                ErrorMessage = "删除会话失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">发送消息请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    [HttpPost("sessions/{sessionId}/messages")]
    [ProducesResponseType(typeof(SendMessageResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> SendMessage(
        string sessionId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            if (session.Status != SessionStatus.Active)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_ACTIVE",
                    ErrorMessage = "会话未激活"
                });
            }
            
            await _dialogService.SendTextAsync(sessionId, request.Text, cancellationToken);
            
            var response = new SendMessageResponse
            {
                SessionId = sessionId,
                MessageId = Guid.NewGuid().ToString(),
                Status = "sent",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Text = request.Text
            };
            
            _logger.LogDebug("文本消息发送成功: {SessionId}", sessionId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送文本消息失败: {SessionId}", sessionId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "MESSAGE_SEND_FAILED",
                ErrorMessage = "发送消息失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 获取会话消息列表
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="limit">限制数量</param>
    /// <param name="offset">偏移量</param>
    /// <returns>消息列表</returns>
    [HttpGet("sessions/{sessionId}/messages")]
    [ProducesResponseType(typeof(MessagesResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> GetMessages(
        string sessionId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            var messages = await _dialogService.GetMessagesAsync(sessionId, limit, offset);
            
            var response = new MessagesResponse
            {
                SessionId = sessionId,
                Messages = messages.ToList(),
                Total = session.Messages.Count,
                Limit = limit,
                Offset = offset
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话消息失败: {SessionId}", sessionId);
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "MESSAGES_GET_FAILED",
                ErrorMessage = "获取消息失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 获取用户会话列表
    /// </summary>
    /// <param name="limit">限制数量</param>
    /// <param name="offset">偏移量</param>
    /// <returns>会话列表</returns>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(SessionsResponse), 200)]
    public async Task<IActionResult> GetUserSessions(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sessions = await _dialogService.GetUserSessionsAsync(userId, limit, offset);
            
            var response = new SessionsResponse
            {
                Sessions = sessions.Select(s => new SessionSummary
                {
                    SessionId = s.SessionId,
                    BotName = s.Config.BotName,
                    Status = s.Status.ToString().ToLower(),
                    CreatedAt = s.CreatedAt,
                    LastActivity = s.LastActivity,
                    MessageCount = s.Messages.Count
                }).ToList(),
                Total = sessions.Count(),
                Limit = limit,
                Offset = offset
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户会话列表失败");
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "SESSIONS_GET_FAILED",
                ErrorMessage = "获取会话列表失败",
                Details = ex.Message
            });
        }
    }
    
    /// <summary>
    /// 获取会话统计信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>统计信息</returns>
    [HttpGet("sessions/{sessionId}/statistics")]
    [ProducesResponseType(typeof(SessionStatisticsResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> GetSessionStatistics(string sessionId)
    {
        try
        {
            var session = await _dialogService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SESSION_NOT_FOUND",
                    ErrorMessage = "会话不存在"
                });
            }
            
            if (!CanAccessSession(session))
            {
                return Forbid();
            }
            
            var audioStatistics = await _dialogService.GetSessionStatisticsAsync(sessionId);
            
            var statistics = new SessionStatistics
            {
                TotalMessages = session.Messages.Count,
                UserMessages = session.Messages.Count(m => m.IsFromUser),
                SystemMessages = session.Messages.Count(m => !m.IsFromUser),
                DurationSeconds = session.EndedAt.HasValue 
                    ? (session.EndedAt.Value - session.CreatedAt).TotalSeconds
                    : (DateTime.UtcNow - session.CreatedAt).TotalSeconds,
                AudioStatistics = audioStatistics,
                CreatedAt = session.CreatedAt,
                LastActivity = session.LastActivity
            };
            
            var response = new SessionStatisticsResponse
            {
                SessionId = sessionId,
                Statistics = statistics
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话统计信息失败: {SessionId}", sessionId);
            return StatusCode(500, new ErrorResponse
            {
                ErrorCode = "STATISTICS_GET_FAILED",
                ErrorMessage = "获取统计信息失败",
                Details = ex.Message
            });
        }
    }
    
    #region Private Methods
    
    private string GetCurrentUserId()
    {
        // 从JWT令牌或用户上下文中获取用户ID
        return User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value ?? "anonymous";
    }
    
    private string GetCurrentUserName()
    {
        // 从JWT令牌或用户上下文中获取用户名
        return User.FindFirst("name")?.Value ?? User.FindFirst("username")?.Value ?? "Anonymous";
    }
    
    private bool CanAccessSession(DialogSession session)
    {
        // 检查当前用户是否有权限访问该会话
        var currentUserId = GetCurrentUserId();
        return session.UserId == currentUserId;
    }
    
    #endregion
}

#region Request/Response Models

/// <summary>
/// 创建会话请求
/// </summary>
public class CreateSessionRequest
{
    /// <summary>
    /// 机器人名称
    /// </summary>
    [Required]
    public string BotName { get; set; } = string.Empty;
    
    /// <summary>
    /// 系统角色
    /// </summary>
    public string? SystemRole { get; set; }
    
    /// <summary>
    /// 音频配置
    /// </summary>
    public Models.Audio.AudioConfig? AudioConfig { get; set; }
    
    /// <summary>
    /// 语音配置
    /// </summary>
    public VoiceConfig? VoiceConfig { get; set; }
    
    /// <summary>
    /// 语言
    /// </summary>
    public string? Language { get; set; }
    
    /// <summary>
    /// 时区
    /// </summary>
    public string? Timezone { get; set; }
}

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
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 会话配置
    /// </summary>
    public SessionConfig Config { get; set; } = null!;
}

/// <summary>
/// 会话响应
/// </summary>
public class SessionResponse
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
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndedAt { get; set; }
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivity { get; set; }
    
    /// <summary>
    /// 会话配置
    /// </summary>
    public SessionConfig Config { get; set; } = null!;
    
    /// <summary>
    /// 消息数量
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
/// 会话操作响应
/// </summary>
public class SessionActionResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 操作类型
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 发送消息请求
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// 文本内容
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 发送消息响应
/// </summary>
public class SendMessageResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 消息ID
    /// </summary>
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 消息列表响应
/// </summary>
public class MessagesResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 消息列表
    /// </summary>
    public List<SessionMessage> Messages { get; set; } = new();
    
    /// <summary>
    /// 总数
    /// </summary>
    public int Total { get; set; }
    
    /// <summary>
    /// 限制数量
    /// </summary>
    public int Limit { get; set; }
    
    /// <summary>
    /// 偏移量
    /// </summary>
    public int Offset { get; set; }
}

/// <summary>
/// 会话列表响应
/// </summary>
public class SessionsResponse
{
    /// <summary>
    /// 会话列表
    /// </summary>
    public List<SessionSummary> Sessions { get; set; } = new();
    
    /// <summary>
    /// 总数
    /// </summary>
    public int Total { get; set; }
    
    /// <summary>
    /// 限制数量
    /// </summary>
    public int Limit { get; set; }
    
    /// <summary>
    /// 偏移量
    /// </summary>
    public int Offset { get; set; }
}

/// <summary>
/// 会话摘要
/// </summary>
public class SessionSummary
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 机器人名称
    /// </summary>
    public string BotName { get; set; } = string.Empty;
    
    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivity { get; set; }
    
    /// <summary>
    /// 消息数量
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
/// 会话统计信息
/// </summary>
public class SessionStatistics
{
    /// <summary>
    /// 总消息数
    /// </summary>
    public int TotalMessages { get; set; }
    
    /// <summary>
    /// 用户消息数
    /// </summary>
    public int UserMessages { get; set; }
    
    /// <summary>
    /// 系统消息数
    /// </summary>
    public int SystemMessages { get; set; }
    
    /// <summary>
    /// 会话持续时间（秒）
    /// </summary>
    public double DurationSeconds { get; set; }
    
    /// <summary>
    /// 音频统计信息
    /// </summary>
    public AudioStatistics? AudioStatistics { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// 会话统计信息响应
/// </summary>
public class SessionStatisticsResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 统计信息
    /// </summary>
    public SessionStatistics Statistics { get; set; } = null!;
}

/// <summary>
/// 错误响应
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// 详细信息
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion