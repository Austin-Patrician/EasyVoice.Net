using EasyVoice.RealtimeDialog;

namespace EasyVoice.RealtimeDialog.TestConsole;

/// <summary>
/// 配置类，包含WebSocket连接配置和音频配置
/// </summary>
public static class Config
{
    /// <summary>
    /// WebSocket连接配置
    /// </summary>
    public static readonly Dictionary<string, object> WsConnectConfig = new()
    {
        ["base_url"] = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue",
        ["headers"] = new Dictionary<string, string>
        {
            ["X-Api-App-ID"] = "7482136989",
            ["X-Api-Access-Key"] = "4akGrrTRlikgCCxBVSi0f3gXQ2uGR8bt",
            ["X-Api-Resource-Id"] = "volc.speech.dialog",
            ["X-Api-App-Key"] = "PlgvMymc7f3tQnJ6",
            ["X-Api-Connect-Id"] = Guid.NewGuid().ToString()
        }
    };

    /// <summary>
    /// 会话启动请求配置
    /// </summary>
    public static readonly object StartSessionReq = new
    {
        tts = new
        {
            audio_config = new
            {
                channel = 1,
                format = "pcm",
                sample_rate = 24000
            }
        },
        dialog = new
        {
            bot_name = "豆包",
            system_role = "你使用活泼灵动的女声，性格开朗，热爱生活。",
            speaking_style = "你的说话风格简洁明了，语速适中，语调自然。",
            extra = new
            {
                strict_audit = false,
                audit_response = "支持客户自定义安全审核回复话术。"
            }
        }
    };

    /// <summary>
    /// 输入音频配置
    /// </summary>
    public static readonly AudioConfigData InputAudioConfig = new()
    {
        Chunk = 3200,
        Format = "pcm",
        Channels = 1,
        SampleRate = 16000,
        BitSize = 16
    };

    /// <summary>
    /// 输出音频配置
    /// </summary>
    public static readonly AudioConfigData OutputAudioConfig = new()
    {
        Chunk = 3200,
        Format = "pcm",
        Channels = 1,
        SampleRate = 24000,
        BitSize = 32
    };
}