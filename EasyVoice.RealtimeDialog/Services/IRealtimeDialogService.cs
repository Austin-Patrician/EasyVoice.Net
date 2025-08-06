using EasyVoice.RealtimeDialog.Models.Audio;
using EasyVoice.RealtimeDialog.Models.Protocol;
using EasyVoice.RealtimeDialog.Models.Session;

namespace EasyVoice.RealtimeDialog.Services;

/// <summary>
/// 实时对话服务接口
/// </summary>
public interface IRealtimeDialogService
{
    /// <summary>
    /// 创建新的对话会话
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="config">会话配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息</returns>
    Task<DialogSession> CreateSessionAsync(string userId, SessionConfig config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话信息</returns>
    Task<DialogSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 开始会话连接
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接任务</returns>
    Task StartSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="reason">结束原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结束任务</returns>
    Task EndSessionAsync(string sessionId, string? reason = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioChunk">音频块</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    Task SendAudioAsync(string sessionId, AudioChunk audioChunk, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送文本消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="text">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    Task SendTextAsync(string sessionId, string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取会话消息历史
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="limit">消息数量限制</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<IEnumerable<SessionMessage>> GetMessagesAsync(string sessionId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取用户的会话列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="limit">会话数量限制</param>
    /// <param name="offset">偏移量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表</returns>
    Task<IEnumerable<DialogSession>> GetUserSessionsAsync(string userId, int limit = 20, int offset = 0, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除任务</returns>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取会话统计信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计信息</returns>
    Task<AudioStatistics?> GetSessionStatisticsAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 会话状态变化事件
    /// </summary>
    event EventHandler<SessionStatusChangedEventArgs> SessionStatusChanged;
    
    /// <summary>
    /// 接收到服务器响应事件
    /// </summary>
    event EventHandler<ServerResponseEventArgs> ServerResponseReceived;
    
    /// <summary>
    /// 音频数据接收事件
    /// </summary>
    event EventHandler<AudioDataEventArgs> AudioDataReceived;
    
    /// <summary>
    /// 错误发生事件
    /// </summary>
    event EventHandler<ErrorEventArgs> ErrorOccurred;
}

/// <summary>
/// 会话状态变化事件参数
/// </summary>
public class SessionStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 旧状态
    /// </summary>
    public SessionStatus OldStatus { get; set; }
    
    /// <summary>
    /// 新状态
    /// </summary>
    public SessionStatus NewStatus { get; set; }
    
    /// <summary>
    /// 变化时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 变化原因
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// 服务器响应事件参数
/// </summary>
public class ServerResponseEventArgs : EventArgs
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 响应消息
    /// </summary>
    public ProtocolMessage Message { get; set; } = null!;
    
    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 音频数据事件参数
/// </summary>
public class AudioDataEventArgs : EventArgs
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// 音频块
    /// </summary>
    public AudioChunk AudioChunk { get; set; } = null!;
    
    /// <summary>
    /// 是否为输入音频（true）还是输出音频（false）
    /// </summary>
    public bool IsInput { get; set; }
    
    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 错误事件参数
/// </summary>
public class ErrorEventArgs : EventArgs
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
    /// 是否可重试
    /// </summary>
    public bool IsRetryable { get; set; }
    
    /// <summary>
    /// 错误时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 错误详情
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}