using EasyVoice.RealtimeDialog.Models.Session;
using EasyVoice.RealtimeDialog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace EasyVoice.RealtimeDialog.Hubs;

/// <summary>
/// 实时对话 SignalR Hub
/// </summary>
[Authorize]
public class RealtimeDialogHub : Hub
{
    private readonly ILogger<RealtimeDialogHub> _logger;
    private readonly IRealtimeDialogService _dialogService;
    
    public RealtimeDialogHub(
        ILogger<RealtimeDialogHub> logger,
        IRealtimeDialogService dialogService)
    {
        _logger = logger;
        _dialogService = dialogService;
    }
    
    /// <summary>
    /// 连接建立时
    /// </summary>
    /// <returns></returns>
    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("用户 {UserId} 连接到实时对话Hub", userId);
        
        // 将用户添加到用户特定的组
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        
        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// 连接断开时
    /// </summary>
    /// <param name="exception">异常信息</param>
    /// <returns></returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("用户 {UserId} 断开连接", userId);
        
        // 从用户特定的组中移除
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// 加入会话组
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task JoinSession(string sessionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var session = await _dialogService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                await SendErrorToClient("SESSION_NOT_FOUND", "会话不存在");
                return;
            }
            
            // 验证用户是否有权限访问该会话
            if (session.UserId != userId)
            {
                await SendErrorToClient("ACCESS_DENIED", "无权访问该会话");
                return;
            }
            
            // 将连接添加到会话特定的组
            await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");
            
            _logger.LogInformation("用户 {UserId} 加入会话 {SessionId}", userId, sessionId);
            
            // 通知客户端已成功加入会话
            await Clients.Caller.SendAsync("SessionJoined", new
            {
                SessionId = sessionId,
                Status = session.Status.ToString().ToLower(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加入会话失败: {SessionId}", sessionId);
            await SendErrorToClient("JOIN_SESSION_FAILED", "加入会话失败", ex.Message);
        }
    }
    
    /// <summary>
    /// 离开会话组
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task LeaveSession(string sessionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // 从会话特定的组中移除连接
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");
            
            _logger.LogInformation("用户 {UserId} 离开会话 {SessionId}", userId, sessionId);
            
            // 通知客户端已成功离开会话
            await Clients.Caller.SendAsync("SessionLeft", new
            {
                SessionId = sessionId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "离开会话失败: {SessionId}", sessionId);
            await SendErrorToClient("LEAVE_SESSION_FAILED", "离开会话失败", ex.Message);
        }
    }
    
    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="text">文本内容</param>
    /// <returns></returns>
    public async Task SendText(string sessionId, string text)
    {
        try
        {
            var userId = GetCurrentUserId();
            var session = await _dialogService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                await SendErrorToClient("SESSION_NOT_FOUND", "会话不存在");
                return;
            }
            
            // 验证用户是否有权限访问该会话
            if (session.UserId != userId)
        {
            await SendErrorToClient("ACCESS_DENIED", "无权访问该会话");
            return;
        }
            
            // 检查会话状态
            if (session.Status != SessionStatus.Active)
            {
                await SendErrorToClient("SESSION_NOT_ACTIVE", "会话未激活");
                return;
            }
            
            // 发送文本消息
            await _dialogService.SendTextAsync(sessionId, text);
            
            _logger.LogDebug("用户 {UserId} 在会话 {SessionId} 中发送文本消息", userId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送文本消息失败: {SessionId}", sessionId);
            await SendErrorToClient("SEND_TEXT_FAILED", "发送文本消息失败", ex.Message);
        }
    }
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioData">音频数据</param>
    /// <returns></returns>
    public async Task SendAudio(string sessionId, byte[] audioData)
    {
        try
        {
            var userId = GetCurrentUserId();
            var session = await _dialogService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                await SendErrorToClient("SESSION_NOT_FOUND", "会话不存在");
                return;
            }
            
            // 验证用户是否有权限访问该会话
        if (session.UserId != userId)
        {
            await SendErrorToClient("ACCESS_DENIED", "无权访问该会话");
            return;
        }
            
            // 检查会话状态
            if (session.Status != SessionStatus.Active)
            {
                await SendErrorToClient("SESSION_NOT_ACTIVE", "会话未激活");
                return;
            }
            
            // 检查音频数据
            if (audioData == null || audioData.Length == 0)
            {
                await SendErrorToClient("INVALID_AUDIO_DATA", "无效的音频数据");
                return;
            }
            
            // 发送音频数据
            await _dialogService.SendAudioAsync(sessionId, audioData);
            
            _logger.LogDebug("用户 {UserId} 在会话 {SessionId} 中发送音频数据: {Size} 字节", 
                userId, sessionId, audioData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频数据失败: {SessionId}", sessionId);
            await SendErrorToClient("SEND_AUDIO_FAILED", "发送音频数据失败", ex.Message);
        }
    }
    
    /// <summary>
    /// 启动会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task StartSession(string sessionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var session = await _dialogService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                await SendErrorToClient("SESSION_NOT_FOUND", "会话不存在");
                return;
            }
            
            // 验证用户是否有权限访问该会话
            if (session.UserId != userId)
            {
                await SendErrorToClient("ACCESS_DENIED", "无权访问该会话");
                return;
            }
            
            // 启动会话
            await _dialogService.StartSessionAsync(sessionId);
            
            _logger.LogInformation("用户 {UserId} 启动会话 {SessionId}", userId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动会话失败: {SessionId}", sessionId);
            await SendErrorToClient("START_SESSION_FAILED", "启动会话失败", ex.Message);
        }
    }
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns></returns>
    public async Task EndSession(string sessionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var session = await _dialogService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                await SendErrorToClient("SESSION_NOT_FOUND", "会话不存在");
                return;
            }
            
            // 验证用户是否有权限访问该会话
            if (session.UserId != userId)
            {
                await SendErrorToClient("ACCESS_DENIED", "无权访问该会话");
                return;
            }
            
            // 结束会话
            await _dialogService.EndSessionAsync(sessionId);
            
            _logger.LogInformation("用户 {UserId} 结束会话 {SessionId}", userId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "结束会话失败: {SessionId}", sessionId);
            await SendErrorToClient("END_SESSION_FAILED", "结束会话失败", ex.Message);
        }
    }
    
    #region Private Methods
    
    /// <summary>
    /// 获取当前用户ID
    /// </summary>
    /// <returns>用户ID</returns>
    private string GetCurrentUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               Context.User?.FindFirst("sub")?.Value ?? 
               Context.User?.FindFirst("user_id")?.Value ?? 
               "anonymous";
    }
    
    /// <summary>
    /// 向客户端发送错误消息
    /// </summary>
    /// <param name="errorCode">错误代码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="details">详细信息</param>
    /// <returns></returns>
    private async Task SendErrorToClient(string errorCode, string errorMessage, string? details = null)
    {
        await Clients.Caller.SendAsync("Error", new
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }
    
    #endregion
}

/// <summary>
/// SignalR Hub 扩展方法
/// </summary>
public static class RealtimeDialogHubExtensions
{
    /// <summary>
    /// 向指定会话的所有连接发送服务器响应
    /// </summary>
    /// <param name="hubContext">Hub上下文</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="response">服务器响应</param>
    /// <returns></returns>
    public static async Task SendServerResponseToSession(
        this IHubContext<RealtimeDialogHub> hubContext,
        string sessionId,
        ServerResponse response)
    {
        await hubContext.Clients.Group($"session_{sessionId}")
            .SendAsync("ServerResponse", response);
    }
    
    /// <summary>
    /// 向指定会话的所有连接发送音频数据
    /// </summary>
    /// <param name="hubContext">Hub上下文</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioData">音频数据</param>
    /// <returns></returns>
    public static async Task SendAudioDataToSession(
        this IHubContext<RealtimeDialogHub> hubContext,
        string sessionId,
        AudioDataResponse audioData)
    {
        await hubContext.Clients.Group($"session_{sessionId}")
            .SendAsync("AudioData", audioData);
    }
    
    /// <summary>
    /// 向指定会话的所有连接发送会话状态变更通知
    /// </summary>
    /// <param name="hubContext">Hub上下文</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="status">会话状态</param>
    /// <returns></returns>
    public static async Task SendSessionStatusChangeToSession(
        this IHubContext<RealtimeDialogHub> hubContext,
        string sessionId,
        SessionStatus status)
    {
        await hubContext.Clients.Group($"session_{sessionId}")
            .SendAsync("SessionStatusChanged", new
            {
                SessionId = sessionId,
                Status = status.ToString().ToLower(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
    }
    
    /// <summary>
    /// 向指定用户的所有连接发送会话状态变更通知
    /// </summary>
    /// <param name="hubContext">Hub上下文</param>
    /// <param name="userId">用户ID</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="status">会话状态</param>
    /// <returns></returns>
    public static async Task SendSessionStatusChangeToUser(
        this IHubContext<RealtimeDialogHub> hubContext,
        string userId,
        string sessionId,
        SessionStatus status)
    {
        await hubContext.Clients.Group($"user_{userId}")
            .SendAsync("SessionStatusChanged", new
            {
                SessionId = sessionId,
                Status = status.ToString().ToLower(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
    }
}

/// <summary>
/// 服务器响应
/// </summary>
public class ServerResponse
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
    /// 响应类型
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// 文本内容
    /// </summary>
    public string? Text { get; set; }
    
    /// <summary>
    /// 是否是最终响应
    /// </summary>
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 音频数据响应
/// </summary>
public class AudioDataResponse
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
    /// 音频数据
    /// </summary>
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// 音频格式
    /// </summary>
    public string Format { get; set; } = "wav";
    
    /// <summary>
    /// 采样率
    /// </summary>
    public int SampleRate { get; set; } = 16000;
    
    /// <summary>
    /// 是否是最终音频片段
    /// </summary>
    public bool IsFinal { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}