using EasyVoice.Core.Interfaces.Tts;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EasyVoice.Infrastructure.Tts.Engines;

/// <summary>
/// EdgeTTS 引擎 - 完美复刻 Node.js 版本的实现
/// 解决 WebSocket 数据处理导致的音质问题
/// </summary>
public class EdgeTtsEngine : ITtsEngine
{
    public string Name => "edge";

    private const string ChromiumFullVersion = "130.0.2849.68";
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const long WindowsFileTimeEpoch = 11644473600L;
    private const int DefaultTimeout = 30000; // 30 seconds, same as Node.js

    public EdgeTtsEngine()
    {
    }

    public async Task<TtsEngineResult> SynthesizeAsync(TtsEngineRequest request, CancellationToken cancellationToken = default)
    {
        var audioFilePath = $"{request.OutputPath}.mp3";
        var subtitleFilePath = $"{request.OutputPath}.json"; // 使用 JSON 格式，与 Node.js 一致
        
        Directory.CreateDirectory(Path.GetDirectoryName(audioFilePath) ?? "");

        // 使用完全复刻的 WebSocket 处理逻辑
        await ProcessTtsRequest(request, audioFilePath, subtitleFilePath, "file", cancellationToken);

        return new TtsEngineResult(audioFilePath, subtitleFilePath);
    }

    public async Task<Stream> SynthesizeStreamAsync(TtsEngineRequest request, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        await ProcessTtsRequest(request, null, null, "stream", cancellationToken, memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// 核心 TTS 处理方法 - 完全复刻 Node.js 的 ttsPromise 方法
    /// </summary>
    private async Task ProcessTtsRequest(
        TtsEngineRequest request, 
        string? audioPath, 
        string? subtitlePath, 
        string outputType,
        CancellationToken cancellationToken,
        Stream? outputStream = null)
    {
        using var webSocket = new ClientWebSocket();
        
        // 配置 WebSocket - 完全按照 Node.js 版本
        ConfigureWebSocket(webSocket);

        // 生成 WebSocket URL
        var wsUrl = GenerateWebSocketUrl();

        // 连接 WebSocket
        using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        await webSocket.ConnectAsync(new Uri(wsUrl), combinedCts.Token);

        // 发送配置消息
        await SendConfigurationMessage(webSocket, combinedCts.Token);

        // 发送 SSML 请求
        var requestId = GenerateRequestId();
        await SendSsmlMessage(webSocket, request, requestId, combinedCts.Token);

        // 处理 WebSocket 响应 - 完全复刻 Node.js 逻辑
        await ProcessWebSocketMessages(webSocket, request.Text, audioPath, subtitlePath, outputType, outputStream, combinedCts.Token);
    }

    /// <summary>
    /// 配置 WebSocket
    /// </summary>
    private static void ConfigureWebSocket(ClientWebSocket webSocket)
    {
        webSocket.Options.SetRequestHeader("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
        webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
    }

    /// <summary>
    /// 生成 WebSocket URL
    /// </summary>
    private static string GenerateWebSocketUrl()
    {
        var secMsGecToken = GenerateSecMsGecToken();
        return $"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
               $"?TrustedClientToken={TrustedClientToken}" +
               $"&Sec-MS-GEC={secMsGecToken}" +
               $"&Sec-MS-GEC-Version=1-{ChromiumFullVersion}";
    }

    /// <summary>
    /// 生成 Sec-MS-GEC Token
    /// </summary>
    private static string GenerateSecMsGecToken()
    {
        var ticks = (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + WindowsFileTimeEpoch) * 10000000L;
        var roundedTicks = ticks - (ticks % 3000000000L);
        var strToHash = $"{roundedTicks}{TrustedClientToken}";

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 生成请求 ID
    /// </summary>
    private static string GenerateRequestId()
    {
        var randomBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    /// <summary>
    /// 发送配置消息
    /// </summary>
    private static async Task SendConfigurationMessage(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var configMessage = 
            "Content-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n" +
            "{\r\n" +
            "  \"context\": {\r\n" +
            "    \"synthesis\": {\r\n" +
            "      \"audio\": {\r\n" +
            "        \"metadataoptions\": {\r\n" +
            "          \"sentenceBoundaryEnabled\": \"false\",\r\n" +
            "          \"wordBoundaryEnabled\": \"true\"\r\n" +
            "        },\r\n" +
            "        \"outputFormat\": \"audio-24khz-96kbitrate-mono-mp3\"\r\n" +
            "      }\r\n" +
            "    }\r\n" +
            "  }\r\n" +
            "}";

        var configBytes = Encoding.UTF8.GetBytes(configMessage);
        await webSocket.SendAsync(configBytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>
    /// 发送 SSML 消息 
    /// </summary>
    private static async Task SendSsmlMessage(ClientWebSocket webSocket, TtsEngineRequest request, string requestId, CancellationToken cancellationToken)
    {
        var lang = ExtractLanguageFromVoice(request.Voice);
        
        // escapeSSML 逻辑
        var escapedText = EscapeSSML(request.Text);
        
        // 处理 prosody 参数
        var rate = NormalizeProsodyValue(request.Rate);
        var pitch = NormalizeProsodyValue(request.Pitch);
        var volume = NormalizeProsodyValue(request.Volume);

        var ssml = $@"<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xmlns:mstts=""https://www.w3.org/2001/mstts"" xml:lang=""{lang}"">
  <voice name=""{request.Voice}"">
    <prosody rate=""{rate}"" pitch=""{pitch}"" volume=""{volume}"">
      {escapedText}
    </prosody>
  </voice>
</speak>";

        var ssmlMessage = $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nPath:ssml\r\n\r\n{ssml}";
        var ssmlBytes = Encoding.UTF8.GetBytes(ssmlMessage);
        
        await webSocket.SendAsync(ssmlBytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>
    /// escapeSSML 逻辑
    /// </summary>
    private static string EscapeSSML(string text)
    {
        return text
            .Replace("&", "&")
            .Replace("<", "<")
            .Replace(">", ">")
            .Replace("\"", "")
            .Replace("'", "'");
    }

    /// <summary>
    /// 标准化 prosody 值 - 复刻 Node.js 逻辑
    /// </summary>
    private static string NormalizeProsodyValue(string? value)
    {
        return string.IsNullOrEmpty(value) || value == "default" ? "default" : value;
    }

    /// <summary>
    /// 从语音名称提取语言代码
    /// </summary>
    private static string ExtractLanguageFromVoice(string voice)
    {
        var match = Regex.Match(voice, @"^([a-zA-Z]{2,5}-[a-zA-Z]{2,5})");
        return match.Success ? match.Groups[1].Value : "en-US";
    }

    /// <summary>
    /// 处理 WebSocket 消息
    /// </summary>
    private static async Task ProcessWebSocketMessages(
        ClientWebSocket webSocket,
        string originalText,
        string? audioPath,
        string? subtitlePath,
        string outputType,
        Stream? outputStream,
        CancellationToken cancellationToken)
    {
        var audioChunks = new List<byte[]>();
        var subtitleLines = new List<SubtitleLine>();
        
        // 使用大缓冲区，避免消息分片
        var messageBuffer = new List<byte>();
        var receiveBuffer = new byte[64 * 1024]; // 64KB 缓冲区

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // 累积消息数据，直到收到完整消息
                messageBuffer.AddRange(receiveBuffer.Take(result.Count));

                // 只有在消息完成时才处理
                if (result.EndOfMessage)
                {
                    var completeMessage = messageBuffer.ToArray();
                    messageBuffer.Clear();

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // 处理二进制音频数据 - 完全复刻 Node.js 逻辑
                        ProcessBinaryMessage(completeMessage, audioChunks, outputStream);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // 处理文本消息 - 完全复刻 Node.js 逻辑
                        var messageText = Encoding.UTF8.GetString(completeMessage);
                        var shouldEnd = ProcessTextMessage(messageText, subtitleLines);
                        
                        if (shouldEnd)
                        {
                            break;
                        }
                    }
                }
            }
        }
        finally
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Completed", CancellationToken.None);
            }
        }

        // 保存结果 - 复刻 Node.js 逻辑
        if (outputType == "file")
        {
            await SaveAudioFile(audioChunks, audioPath!);
            await SaveSubtitleFile(subtitleLines, originalText, subtitlePath!);
        }
    }

    /// <summary>
    /// 处理二进制消息
    /// </summary>
    private static void ProcessBinaryMessage(byte[] data, List<byte[]> audioChunks, Stream? outputStream)
    {
        // 复刻 Node.js 的音频数据分离逻辑
        var separator = Encoding.UTF8.GetBytes("Path:audio\r\n");
        var separatorIndex = IndexOfBytes(data, separator);
        
        if (separatorIndex != -1)
        {
            var audioStartIndex = separatorIndex + separator.Length;
            var audioData = new byte[data.Length - audioStartIndex];
            Array.Copy(data, audioStartIndex, audioData, 0, audioData.Length);
            
            if (outputStream != null)
            {
                // 流模式：实时写入
                outputStream.Write(audioData, 0, audioData.Length);
            }
            else
            {
                // 文件模式：累积数据
                audioChunks.Add(audioData);
            }
        }
    }

    /// <summary>
    /// 处理文本消息
    /// </summary>
    private static bool ProcessTextMessage(string message, List<SubtitleLine> subtitleLines)
    {
        if (message.Contains("Path:turn.end"))
        {
            return true; // 结束标志
        }
        
        if (message.Contains("Path:audio.metadata"))
        {
            ParseSubtitleMetadata(message, subtitleLines);
        }
        
        return false;
    }

    /// <summary>
    /// 高效的字节序列查找
    /// </summary>
    private static int IndexOfBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }

    /// <summary>
    /// 解析字幕元数据
    /// </summary>
    private static void ParseSubtitleMetadata(string message, List<SubtitleLine> subtitleLines)
    {
        try
        {
            var lines = message.Split("\r\n");
            var jsonLine = lines.LastOrDefault(line => line.Trim().StartsWith("{"));
            
            if (jsonLine != null)
            {
                var metadata = JsonSerializer.Deserialize<JsonElement>(jsonLine);
                
                if (metadata.TryGetProperty("Metadata", out var metadataArray))
                {
                    foreach (var element in metadataArray.EnumerateArray())
                    {
                        if (element.TryGetProperty("Data", out var data) &&
                            data.TryGetProperty("text", out var textData) &&
                            textData.TryGetProperty("Text", out var textElement) &&
                            data.TryGetProperty("Offset", out var offsetElement) &&
                            data.TryGetProperty("Duration", out var durationElement))
                        {
                            var text = textElement.GetString() ?? "";
                            var offset = offsetElement.GetInt64();
                            var duration = durationElement.GetInt64();
                            
                            subtitleLines.Add(new SubtitleLine
                            {
                                Part = text,
                                Start = offset / 10000, // Convert from 100ns to ms 
                                End = (offset + duration) / 10000
                            });
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 忽略 JSON 解析错误
        }
    }

    /// <summary>
    /// 保存音频文件
    /// </summary>
    private static async Task SaveAudioFile(List<byte[]> audioChunks, string audioPath)
    {
        if (audioChunks.Count > 0)
        {
            var totalLength = audioChunks.Sum(chunk => chunk.Length);
            var allAudioData = new byte[totalLength];
            var offset = 0;
            
            foreach (var chunk in audioChunks)
            {
                Array.Copy(chunk, 0, allAudioData, offset, chunk.Length);
                offset += chunk.Length;
            }
            
            await File.WriteAllBytesAsync(audioPath, allAudioData);
        }
    }

    /// <summary>
    /// 保存字幕文件
    /// </summary>
    private static async Task SaveSubtitleFile(List<SubtitleLine> subtitleLines, string originalText, string subtitlePath)
    {
        if (subtitleLines.Count == 0)
        {
            // 创建空的字幕文件
            await File.WriteAllTextAsync(subtitlePath, "[]");
            return;
        }

        // 字符匹配
        var subChars = originalText.ToCharArray();
        var subCharIndex = 0;
        
        for (int i = 0; i < subtitleLines.Count; i++)
        {
            var cue = subtitleLines[i];
            var fullPart = "";
            var stepIndex = 0;
            
            for (int sci = subCharIndex; sci < subChars.Length; sci++)
            {
                if (stepIndex < cue.Part.Length && subChars[sci] == cue.Part[stepIndex])
                {
                    fullPart += subChars[sci];
                    stepIndex++;
                }
                else if (i + 1 < subtitleLines.Count && 
                         subtitleLines[i + 1].Part.Length > 0 && 
                         subChars[sci] == subtitleLines[i + 1].Part[0])
                {
                    subCharIndex = sci;
                    break;
                }
                else
                {
                    fullPart += subChars[sci];
                }
            }
            
            cue.Part = fullPart;
        }

        // 保存为 JSON 格式 - 与 Node.js 一致
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(subtitleLines, options);
        await File.WriteAllTextAsync(subtitlePath, json);
    }

    /// <summary>
    /// 字幕行数据结构
    /// </summary>
    private class SubtitleLine
    {
        public string Part { get; set; } = "";
        public long Start { get; set; }
        public long End { get; set; }
    }
}
