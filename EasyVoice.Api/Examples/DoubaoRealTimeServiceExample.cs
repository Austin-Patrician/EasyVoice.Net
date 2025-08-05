using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using ErrorEventArgs = EasyVoice.Core.Models.ErrorEventArgs;

namespace EasyVoice.Api.Examples;

/// <summary>
/// DoubaoRealTimeService 使用示例
/// 演示如何在C#代码中直接使用实时语音对话服务
/// </summary>
public class DoubaoRealTimeServiceExample
{
    private readonly IRealTimeService _realTimeService;
    private readonly ILogger<DoubaoRealTimeServiceExample> _logger;
    private string? _currentSessionId;

    public DoubaoRealTimeServiceExample(IRealTimeService realTimeService, ILogger<DoubaoRealTimeServiceExample> logger)
    {
        _realTimeService = realTimeService;
        _logger = logger;
        SetupEventHandlers();
    }

    /// <summary>
    /// 完整的实时对话流程示例
    /// </summary>
    /// <param name="appId">豆包应用ID</param>
    /// <param name="accessToken">豆包访问令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> RunCompleteDialogExampleAsync(string appId, string accessToken)
    {
        try
        {
            _logger.LogInformation("开始实时语音对话示例");

            // 1. 配置连接参数
            var config = new RealTimeConnectionConfig
            {
                AppId = appId,
                AccessToken = accessToken,
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
    /// 简单的连接测试示例
    /// </summary>
    /// <param name="appId">豆包应用ID</param>
    /// <param name="accessToken">豆包访问令牌</param>
    /// <returns>是否连接成功</returns>
    public async Task<bool> TestConnectionAsync(string appId, string accessToken)
    {
        try
        {
            var config = new RealTimeConnectionConfig
            {
                AppId = appId,
                AccessToken = accessToken
            };

            _logger.LogInformation("测试连接到豆包实时语音服务...");
            var connected = await _realTimeService.ConnectAsync(config);
            
            if (connected)
            {
                _logger.LogInformation("连接测试成功！");
                await _realTimeService.DisconnectAsync();
                return true;
            }
            else
            {
                _logger.LogError("连接测试失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接测试时发生异常");
            return false;
        }
    }

    /// <summary>
    /// 发送音频数据示例
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <returns>是否发送成功</returns>
    public async Task<bool> SendAudioDataAsync(byte[] audioData)
    {
        if (string.IsNullOrEmpty(_currentSessionId))
        {
            _logger.LogError("没有活动的会话，无法发送音频数据");
            return false;
        }

        try
        {
            await _realTimeService.SendAudioDataAsync(_currentSessionId, audioData);
            _logger.LogDebug("发送音频数据: {Length} 字节", audioData.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送音频数据失败");
            return false;
        }
    }

    /// <summary>
    /// 获取连接统计信息示例
    /// </summary>
    /// <returns>连接统计信息</returns>
    public async Task<object?> GetConnectionStatsAsync()
    {
        try
        {
            var stats = await _realTimeService.GetConnectionStatsAsync();
            _logger.LogInformation("连接统计信息: {Stats}", stats);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取连接统计信息失败");
            return null;
        }
    }

    /// <summary>
    /// 设置事件处理器
    /// </summary>
    private void SetupEventHandlers()
    {
        // 连接状态变化事件
        _realTimeService.ConnectionStateChanged += (sender, args) =>
        {
            _logger.LogInformation("连接状态变化: {OldState} -> {NewState}", args.OldState, args.NewState);
        };

        // 音频数据接收事件
        _realTimeService.AudioDataReceived += (sender, args) =>
        {
            _logger.LogDebug("接收到音频数据: {Length} 字节, 格式: {Format}, 采样率: {SampleRate}", 
                args.AudioData.Length, args.Format, args.SampleRate);
            
            // 这里可以处理接收到的音频数据，例如播放或保存
            ProcessReceivedAudio(args.AudioData, args.Format, args.SampleRate);
        };

        // 对话事件
        _realTimeService.DialogEvent += (sender, args) =>
        {
            _logger.LogInformation("对话事件: {EventType}, 会话ID: {SessionId}", args.EventType, args.SessionId);
            
            // 根据事件类型处理不同的对话事件
            HandleDialogEvent(args);
        };

        // 错误事件
        _realTimeService.ErrorOccurred += (sender, args) =>
        {
            _logger.LogError(args.Exception, "发生错误: {ErrorMessage}, 错误代码: {ErrorCode}", 
                args.ErrorMessage, args.ErrorCode);
            
            // 处理错误，例如重连或通知用户
            HandleError(args);
        };
    }

    /// <summary>
    /// 处理接收到的音频数据
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <param name="format">音频格式</param>
    /// <param name="sampleRate">采样率</param>
    private void ProcessReceivedAudio(byte[] audioData, string format, int sampleRate)
    {
        // 实际应用中，这里可以：
        // 1. 播放音频
        // 2. 保存到文件
        // 3. 进行音频处理
        // 4. 转发到其他服务
        
        _logger.LogDebug("处理音频数据: {Length} 字节, {Format}, {SampleRate}Hz", 
            audioData.Length, format, sampleRate);
    }

    /// <summary>
    /// 处理对话事件
    /// </summary>
    /// <param name="args">对话事件参数</param>
    private void HandleDialogEvent(DialogEventArgs args)
    {
        switch (args.EventType)
        {
            case RealTimeEventType.SessionStarted:
                _logger.LogInformation("会话已开始: {SessionId}", args.SessionId);
                break;
            case RealTimeEventType.SessionFinished:
                _logger.LogInformation("会话已结束: {SessionId}", args.SessionId);
                break;
            default:
                _logger.LogInformation("未知对话事件: {EventType}, {SessionId}", args.EventType, args.SessionId);
                break;
        }
    }

    /// <summary>
    /// 处理错误事件
    /// </summary>
    /// <param name="args">错误事件参数</param>
    private void HandleError(ErrorEventArgs args)
    {
        // 根据错误类型进行不同的处理
        switch (args.ErrorCode)
        {
            case 1001: // 连接超时
                _logger.LogWarning("连接超时，尝试重连...");
                // 可以实现自动重连逻辑
                break;
            case 1002: // 认证失败
                _logger.LogError("认证失败，请检查AppId和AccessToken");
                break;
            case 1003: // 会话不存在
                _logger.LogWarning("会话不存在，可能需要重新创建会话");
                break;
            default:
                _logger.LogError("未知错误: {ErrorCode} - {ErrorMessage}", args.ErrorCode, args.ErrorMessage);
                break;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async Task DisposeAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentSessionId))
            {
                await _realTimeService.FinishSessionAsync(_currentSessionId);
            }
            
            await _realTimeService.DisconnectAsync();
            _realTimeService.Dispose();
            
            _logger.LogInformation("DoubaoRealTimeService 资源已释放");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放资源时发生错误");
        }
    }
}

/// <summary>
/// 使用示例的静态工厂类
/// </summary>
public static class DoubaoRealTimeServiceExampleFactory
{
    // /// <summary>
    // /// 创建使用示例实例
    // /// </summary>
    // /// <param name="logger">日志记录器</param>
    // /// <returns>使用示例实例</returns>
    // public static DoubaoRealTimeServiceExample Create(ILogger<DoubaoRealTimeServiceExample> logger)
    // {
    //     // 创建实时服务实例
    //     var realTimeService = new DoubaoRealTimeService(logger);
    //     
    //     // 创建使用示例
    //     return new DoubaoRealTimeServiceExample(realTimeService, logger);
    // }
    //
    // /// <summary>
    // /// 运行完整示例
    // /// </summary>
    // /// <param name="appId">豆包应用ID</param>
    // /// <param name="accessToken">豆包访问令牌</param>
    // /// <param name="logger">日志记录器</param>
    // /// <returns>是否成功</returns>
    // public static async Task<bool> RunExampleAsync(string appId, string accessToken, ILogger<DoubaoRealTimeServiceExample> logger)
    // {
    //     var example = Create(logger);
    //     try
    //     {
    //         return await example.RunCompleteDialogExampleAsync(appId, accessToken);
    //     }
    //     finally
    //     {
    //         await example.DisposeAsync();
    //     }
    // }
}