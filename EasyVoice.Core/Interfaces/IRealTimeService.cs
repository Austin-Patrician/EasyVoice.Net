using EasyVoice.Core.Models;

namespace EasyVoice.Core.Interfaces;

/// <summary>
/// 实时语音对话服务接口
/// </summary>
public interface IRealTimeService : IDisposable
{
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    
    /// <summary>
    /// 音频数据接收事件
    /// </summary>
    event EventHandler<AudioDataEventArgs>? AudioDataReceived;
    
    /// <summary>
    /// 对话事件
    /// </summary>
    event EventHandler<DialogEventArgs>? DialogEvent;
    
    /// <summary>
    /// 错误事件
    /// </summary>
    event EventHandler<EasyVoice.Core.Models.ErrorEventArgs>? ErrorOccurred;
    
    /// <summary>
    /// 当前连接状态
    /// </summary>
    RealTimeDialogState ConnectionState { get; }
    
    /// <summary>
    /// 当前会话信息
    /// </summary>
    SessionInfo? CurrentSession { get; }
    
    /// <summary>
    /// 连接到实时对话服务
    /// </summary>
    /// <param name="config">连接配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接任务</returns>
    Task<bool> ConnectAsync(RealTimeConnectionConfig config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 断开连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>断开连接任务</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 开始会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="payload">会话配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>开始会话任务</returns>
    Task<bool> StartSessionAsync(string sessionId, StartSessionPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 结束会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结束会话任务</returns>
    Task<bool> FinishSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送问候语
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="payload">问候语内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    Task<bool> SayHelloAsync(string sessionId, SayHelloPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送聊天TTS文本
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="payload">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    Task<bool> SendChatTtsTextAsync(string sessionId, ChatTtsTextPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送音频数据
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="audioData">音频数据</param>
    /// <param name="sequence">序列号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送任务</returns>
    Task<bool> SendAudioDataAsync(string sessionId, byte[] audioData, int? sequence = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 开始音频录制
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>录制任务</returns>
    Task StartAudioRecordingAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止音频录制
    /// </summary>
    /// <returns>停止任务</returns>
    Task StopAudioRecordingAsync();
    
    /// <summary>
    /// 开始音频播放
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>播放任务</returns>
    Task StartAudioPlaybackAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止音频播放
    /// </summary>
    /// <returns>停止任务</returns>
    Task StopAudioPlaybackAsync();
    
    /// <summary>
    /// 获取连接统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    Task<Dictionary<string, object>> GetConnectionStatsAsync();
}