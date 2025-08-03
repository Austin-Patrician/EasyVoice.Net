using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using EasyVoice.Core.Interfaces.Tts;
using EasyVoice.Infrastructure.Tts.Protocols;

namespace EasyVoice.Infrastructure.Tts.Engines;

/// <summary>
/// Doubao TTS 引擎配置选项
/// </summary>
public class DoubaoTtsOptions
{
    public string? AppId { get; set; }
    public string? AccessToken { get; set; }
    public string? Cluster { get; set; }
    public string VoiceType { get; set; } = "zh_female_1";
    public string AudioEncoding { get; set; } = "mp3";
    public string Endpoint { get; set; } = "wss://openspeech.bytedance.com/api/v1/tts/ws_binary";
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// 根据语音类型自动选择集群
    /// </summary>
    public string GetCluster()
    {
        if (!string.IsNullOrEmpty(Cluster))
        {
            return Cluster;
        }

        if (VoiceType.StartsWith("S_"))
        {
            return "volcano_icl";
        }

        return "volcano_tts";
    }
}

/// <summary>
/// Doubao TTS 引擎
/// </summary>
public class DoubaoTtsEngine : ITtsEngine
{
    public string Name => "doubao";

    private readonly DoubaoTtsOptions _options;
    private readonly ILogger<DoubaoTtsEngine>? _logger;

    // 支持的语音类型列表
    private static readonly string[] SupportedVoices =
    [
        "zh_female_1", "zh_female_2", "zh_male_1", "zh_male_2",
        "en_female_1", "en_female_2", "en_male_1", "en_male_2",
        "S_zh_female_1", "S_zh_female_2", "S_zh_male_1", "S_zh_male_2",
        "zh_female_wanqudashu_moon_bigtts", "zh_female_daimengchuanmei_moon_bigtts", "zh_male_guozhoudede_moon_bigtts",
        "zh_male_beijingxiaoye_moon_bigtts", "zh_male_shaonianzixin_moon_bigtts", "zh_female_meilinyou_moon_bigtts",
        "zh_male_shenyeboke_moon_bigtts", "zh_female_sajiaonvyou_moon_bigtts", "zh_female_yuanqinyvyou_moon_bigtts",
        "zh_male_haoyuxiaoge_moon_bigtts",
        "zh_female_wanqudashu_moon_bigtts", "zh_female_daimengchuanmei_moon_bigtts", "zh_male_guozhoudede_moon_bigtts",
        "zh_male_beijingxiaoye_moon_bigtts", "zh_male_shaonianzixin_moon_bigtts", "zh_female_meilinyou_moon_bigtts",
        "zh_male_shenyeboke_moon_bigtts", "zh_female_sajiaonvyou_moon_bigtts", "zh_female_yuanqinyvyou_moon_bigtts",
        "zh_male_haoyuxiaoge_moon_bigtts",
        "zh_female_wanqudashu_moon_bigtts", "zh_female_daimengchuanmei_moon_bigtts", "zh_male_guozhoudede_moon_bigtts",
        "zh_male_beijingxiaoye_moon_bigtts", "zh_male_shaonianzixin_moon_bigtts", "zh_female_meilinyou_moon_bigtts",
        "zh_male_shenyeboke_moon_bigtts", "zh_female_sajiaonvyou_moon_bigtts", "zh_female_yuanqinyvyou_moon_bigtts",
        "zh_male_haoyuxiaoge_moon_bigtts"
    ];

    // 支持的音频格式
    private static readonly string[] SupportedEncodings =
    [
        "wav", "mp3", "aac", "pcm", "ogg_opus"
    ];

    public DoubaoTtsEngine(DoubaoTtsOptions options, ILogger<DoubaoTtsEngine>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        ValidateOptions();
    }

    public async Task<TtsEngineResult> SynthesizeAsync(TtsEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Starting Doubao TTS synthesis for text: {Text}",
                request.Text[..Math.Min(50, request.Text.Length)]);

            // 验证请求
            ValidateRequest(request);

            // 建立 WebSocket 连接并进行TTS合成
            var audioData = await SynthesizeInternal(request.Text, request.Voice, cancellationToken);

            // 保存音频文件
            var audioFormat = ExtractAudioFormat(request.Voice);
            var audioFilePath = $"{request.OutputPath}.{audioFormat}";
            await File.WriteAllBytesAsync(audioFilePath, audioData, cancellationToken);

            // 生成字幕文件
            var subtitleFilePath = $"{request.OutputPath}.srt";
            await GenerateSubtitleFile(subtitleFilePath, request.Text, cancellationToken);

            _logger?.LogInformation("Doubao TTS synthesis completed: {AudioFile}", audioFilePath);

            return new TtsEngineResult(audioFilePath, subtitleFilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to synthesize speech with Doubao TTS");
            throw;
        }
    }

    public async Task<Stream> SynthesizeStreamAsync(TtsEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Starting Doubao TTS streaming synthesis");

            // 验证请求
            ValidateRequest(request);

            // 进行TTS合成
            var audioData = await SynthesizeInternal(request.Text, request.Voice, cancellationToken);

            // 返回音频流
            return new MemoryStream(audioData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to synthesize streaming speech with Doubao TTS");
            throw;
        }
    }

    /// <summary>
    /// 内部合成方法 - 核心 WebSocket 通信逻辑
    /// </summary>
    private async Task<byte[]> SynthesizeInternal(string text, string? voice, CancellationToken cancellationToken)
    {
        using var webSocket = new ClientWebSocket();
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var combinedToken = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token).Token;

        try
        {
            // 设置请求头
            webSocket.Options.SetRequestHeader("Authorization", $"Bearer;{_options.AccessToken}");
            webSocket.Options.CollectHttpResponseDetails = true;

            _logger?.LogDebug("Connecting to Doubao TTS endpoint: {Endpoint}", _options.Endpoint);

            // 连接到服务器
            await webSocket.ConnectAsync(new Uri(_options.Endpoint), combinedToken);

            var responseHeaders = webSocket.HttpResponseHeaders;
            if (responseHeaders != null)
            {
                _logger?.LogDebug("Connection established, Logid: {LogId}", responseHeaders["x-tt-logid"]);
            }

            // 准备请求数据
            var voiceType = ExtractVoice(voice);
            var encoding = ExtractAudioFormat(voice);
            var cluster = _options.GetCluster();

            var request = new Dictionary<string, object>
            {
                ["app"] = new Dictionary<string, object>
                {
                    ["appid"] = _options.AppId!,
                    ["token"] = _options.AccessToken!,
                    ["cluster"] = cluster
                },
                ["user"] = new Dictionary<string, object>
                {
                    ["uid"] = Guid.NewGuid().ToString()
                },
                ["audio"] = new Dictionary<string, object>
                {
                    ["voice_type"] = voiceType,
                    ["encoding"] = encoding
                },
                ["request"] = new Dictionary<string, object>
                {
                    ["reqid"] = Guid.NewGuid().ToString(),
                    ["text"] = text,
                    ["operation"] = "submit",
                    ["with_timestamp"] = "1",
                    ["extra_param"] = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["disable_markdown_filter"] = false,
                    })
                }
            };

            _logger?.LogDebug("Sending TTS request: voice={Voice}, encoding={Encoding}, cluster={Cluster}",
                voiceType, encoding, cluster);

            // 发送文本请求
            var requestPayload = JsonSerializer.SerializeToUtf8Bytes(request);
            await DoubaoClientHelper.FullClientRequest(webSocket, requestPayload, combinedToken);

            // 接收音频数据
            var audio = new List<byte>();

            while (true)
            {
                var message = await DoubaoClientHelper.ReceiveMessage(webSocket, combinedToken);

                _logger?.LogDebug("Received message: {Message}", message.ToString());

                switch (message.MsgType)
                {
                    case MsgType.FrontEndResultServer:
                        // 前端结果，暂时忽略
                        break;
                    case MsgType.AudioOnlyServer:
                        if (message.Payload != null && message.Payload.Length > 0)
                        {
                            audio.AddRange(message.Payload);
                        }
                        break;
                    case MsgType.Error:
                        var errorMessage = message.GetPayloadString();
                        _logger?.LogError("Received error from Doubao TTS: {Error}", errorMessage);
                        throw new Exception($"Doubao TTS error: {errorMessage}");
                    default:
                        _logger?.LogWarning("Received unexpected message type: {MessageType}", message.MsgType);
                        break;
                }

                // 检查是否是最后一个音频包
                if (message.MsgType == MsgType.AudioOnlyServer && message.Sequence < 0)
                {
                    _logger?.LogDebug("Received final audio packet, ending synthesis");
                    break;
                }
            }

            if (audio.Count == 0)
            {
                throw new Exception("No audio data received from Doubao TTS");
            }

            _logger?.LogInformation("Doubao TTS synthesis completed, received {Size} bytes of audio data", audio.Count);
            return audio.ToArray();
        }
        catch (WebSocketException ex)
        {
            _logger?.LogError(ex, "WebSocket connection failed");
            throw new Exception($"Failed to connect to Doubao TTS service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "Doubao TTS request timed out");
            throw new Exception("Doubao TTS request timed out", ex);
        }
        finally
        {
            cancellationTokenSource.Cancel();
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Synthesis completed",
                    CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// 验证配置选项
    /// </summary>
    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.AppId))
        {
            throw new ArgumentException("Doubao TTS requires an App ID.", nameof(_options.AppId));
        }

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new ArgumentException("Doubao TTS requires an Access Token.", nameof(_options.AccessToken));
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new ArgumentException("Doubao TTS requires an Endpoint.", nameof(_options.Endpoint));
        }
    }

    /// <summary>
    /// 验证请求参数
    /// </summary>
    private static void ValidateRequest(TtsEngineRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
        {
            throw new ArgumentException("Input text is required.");
        }

        // Doubao TTS 文本长度限制
        if (request.Text.Length > 300)
        {
            throw new ArgumentException(
                "Input text exceeds 300 characters, which is the maximum allowed by Doubao TTS.");
        }
    }

    /// <summary>
    /// 提取语音类型
    /// </summary>
    private string ExtractVoice(string? voice)
    {
        var selectedVoice = voice ?? _options.VoiceType;

        if (!SupportedVoices.Contains(selectedVoice))
        {
            _logger?.LogWarning("Unsupported voice {Voice}, using default {DefaultVoice}", selectedVoice,
                _options.VoiceType);
            selectedVoice = _options.VoiceType;
        }

        return selectedVoice;
    }

    /// <summary>
    /// 提取音频格式
    /// </summary>
    private string ExtractAudioFormat(string? encoding)
    {
        if (string.IsNullOrEmpty(encoding))
        {
            encoding = _options.AudioEncoding;
        }
        
        if (!SupportedEncodings.Contains(encoding))
        {
            _logger?.LogWarning("Unsupported audio encoding {Format}, using mp3", encoding);
            encoding = "mp3";
        }

        return encoding;
    }

    /// <summary>
    /// 生成字幕文件
    /// </summary>
    private static async Task GenerateSubtitleFile(string subtitleFilePath, string text,
        CancellationToken cancellationToken)
    {
        // 生成简单的 SRT 字幕文件
        var srtContent = new StringBuilder();
        srtContent.AppendLine("1");
        srtContent.AppendLine("00:00:00,000 --> 00:00:10,000");
        srtContent.AppendLine(text);
        srtContent.AppendLine();

        await File.WriteAllTextAsync(subtitleFilePath, srtContent.ToString(), Encoding.UTF8, cancellationToken);
    }
}

/// <summary>
/// DoubaoMessage 扩展方法
/// </summary>
public static class DoubaoMessageExtensions
{
    /// <summary>
    /// 获取载荷字符串表示
    /// </summary>
    public static string GetPayloadString(this DoubaoMessage message)
    {
        if (message.Payload == null || message.Payload.Length == 0)
            return "";
        try
        {
            return Encoding.UTF8.GetString(message.Payload);
        }
        catch
        {
            return Convert.ToHexString(message.Payload);
        }
    }
}