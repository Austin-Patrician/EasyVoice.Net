using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using EasyVoice.Core.Interfaces.Tts;


namespace EasyVoice.Infrastructure.Tts.Engines;

/// <summary>
/// OpenAI TTS 引擎配置选项
/// </summary>
public class OpenAiTtsOptions
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; } = "https://api.openai.com";
    public string ModelName { get; set; } = "tts-1";
    public int TimeoutSeconds { get; set; } = 30;

    public string? Instructions { get; set; }
}

/// <summary>
/// OpenAI TTS 引擎
/// </summary>
public class OpenAiTtsEngine : ITtsEngine
{
    public string Name => "openai";

    private readonly OpenAiTtsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiTtsEngine>? _logger;

    // 复刻 Node.js 版本的常量定义
    private static readonly string[] OpenAiVoices = ["alloy", "echo", "fable", "onyx", "nova", "shimmer"];
    private static readonly string[] ResponseFormats = ["mp3", "opus", "aac", "flac", "wav", "pcm"];

    public OpenAiTtsEngine(OpenAiTtsOptions options, HttpClient httpClient, ILogger<OpenAiTtsEngine>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;

        // 验证 API Key
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ArgumentException("OpenAI TTS requires an API key.", nameof(options.ApiKey));
        }

        // 配置 HttpClient
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl ?? "https://api.openai.com");
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<TtsEngineResult> SynthesizeAsync(TtsEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 参数验证
            ValidateRequest(request);

            // 解析参数
            var voice = ExtractVoice(request.Voice);
            var speed = ExtractSpeed(request.Rate);
            var format = ExtractFormat("mp3"); // 默认使用 mp3 格式

            _logger?.LogInformation(
                "Synthesizing speech with OpenAI TTS: voice={Voice}, speed={Speed}, format={Format}, textLength={TextLength}",
                voice, speed, format, request.Text.Length);

            // 调用 OpenAI API
            var audioData = await CallOpenAiTtsApi(request.Text, voice, speed, format, cancellationToken);

            // 保存音频文件
            var audioFilePath = $"{request.OutputPath}.{format}";
            await File.WriteAllBytesAsync(audioFilePath, audioData, cancellationToken);

            // 生成字幕文件
            var subtitleFilePath = $"{request.OutputPath}.srt";
            await GenerateSubtitleFile(subtitleFilePath, request.Text, cancellationToken);

            _logger?.LogInformation("OpenAI TTS synthesis completed: {AudioFile}", audioFilePath);

            return new TtsEngineResult(audioFilePath, subtitleFilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to synthesize speech with OpenAI TTS for text: {Text}",
                request.Text[..Math.Min(50, request.Text.Length)]);
            throw;
        }
    }

    public async Task<Stream> SynthesizeStreamAsync(TtsEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 参数验证
            ValidateRequest(request);

            // 解析参数
            var voice = ExtractVoice(request.Voice);
            var speed = ExtractSpeed(request.Rate);
            var format = ExtractFormat("mp3");

            _logger?.LogInformation("Synthesizing streaming speech with OpenAI TTS: voice={Voice}, speed={Speed}",
                voice, speed);

            // 调用 OpenAI API
            var audioData = await CallOpenAiTtsApi(request.Text, voice, speed, format, cancellationToken);

            // 返回音频流
            return new MemoryStream(audioData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to synthesize streaming speech with OpenAI TTS");
            throw;
        }
    }

    /// <summary>
    /// 验证请求参数
    /// </summary>
    private static void ValidateRequest(TtsEngineRequest request)
    {
        // 验证文本
        if (string.IsNullOrEmpty(request.Text))
        {
            throw new ArgumentException("Input text is required.");
        }

        //的文本长度限制
        if (request.Text.Length > 4096)
        {
            throw new ArgumentException(
                "Input text exceeds 4096 characters, which is the maximum allowed by OpenAI TTS.");
        }
    }

    /// <summary>
    /// 提取并验证语音参数
    /// </summary>
    private static string ExtractVoice(string? voice)
    {
        var selectedVoice = voice ?? "alloy";

        if (!OpenAiVoices.Contains(selectedVoice))
        {
            throw new ArgumentException(
                $"Invalid voice: {selectedVoice}. Supported voices are: {string.Join(", ", OpenAiVoices)}.");
        }

        return selectedVoice;
    }

    /// <summary>
    /// 提取并验证速度参数
    /// </summary>
    private static double ExtractSpeed(string? rate)
    {
        if (string.IsNullOrEmpty(rate) || rate == "default")
        {
            return 1.0;
        }

        if (double.TryParse(rate, out var speed))
        {
            // 复刻 Node.js 的速度范围验证
            if (speed < 0.25 || speed > 4.0)
            {
                throw new ArgumentException("Speed must be between 0.25 and 4.0.");
            }

            return speed;
        }

        return 1.0; // 默认速度
    }

    /// <summary>
    /// 提取并验证格式参数
    /// </summary>
    private static string ExtractFormat(string format)
    {
        if (!ResponseFormats.Contains(format))
        {
            throw new ArgumentException(
                $"Invalid response format: {format}. Supported formats are: {string.Join(", ", ResponseFormats)}.");
        }

        return format;
    }

    /// <summary>
    /// 调用 OpenAI TTS API
    /// </summary>
    private async Task<byte[]> CallOpenAiTtsApi(string text, string voice, double speed, string format,
        CancellationToken cancellationToken)
    {
        // 构建请求体 - 与 Node.js 版本完全一致
        var requestBody = new
        {
            model = _options.ModelName, // 使用与 Node.js 相同的模型
            input = text,
            voice,
            speed,
            response_format = format
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = new SnakeCaseNamingPolicy()
        });

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            _logger?.LogDebug("Sending request to OpenAI TTS API: {RequestBody}", jsonContent);

            // 发送请求到 OpenAI API
            var response = await _httpClient.PostAsync("/v1/audio/speech", content, cancellationToken);

            // 复刻 Node.js 版本的错误处理
            await HandleApiResponse(response);

            // 读取音频数据
            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger?.LogInformation("Successfully received {AudioDataSize} bytes from OpenAI TTS API",
                audioData.Length);

            return audioData;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP request failed when calling OpenAI TTS API");
            throw new Exception($"Failed to synthesize speech: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "Request to OpenAI TTS API timed out");
            throw new Exception("Request to OpenAI TTS API timed out", ex);
        }
    }

    /// <summary>
    /// 处理 API 响应 
    /// </summary>
    private async Task HandleApiResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorContent = await response.Content.ReadAsStringAsync();

        //特定错误处理
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                _logger?.LogError("OpenAI API authentication failed: {ErrorContent}", errorContent);
                throw new Exception("Invalid OpenAI API key.");

            case HttpStatusCode.TooManyRequests:
                _logger?.LogWarning("OpenAI API rate limit exceeded: {ErrorContent}", errorContent);
                throw new Exception("Rate limit exceeded for OpenAI TTS.");

            case HttpStatusCode.BadRequest:
                _logger?.LogError("Bad request to OpenAI API: {ErrorContent}", errorContent);
                throw new Exception($"Bad request to OpenAI TTS API: {errorContent}");

            default:
                _logger?.LogError("OpenAI API returned error {StatusCode}: {ErrorContent}", response.StatusCode,
                    errorContent);
                throw new Exception($"OpenAI TTS API error ({response.StatusCode}): {errorContent}");
        }
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

    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    public Task<string[]> GetSupportedLanguagesAsync()
    {
        // 复刻 Node.js 版本支持的语言列表
        var supportedLanguages = new[] { "en-US", "zh-CN", "es-ES", "fr-FR", "de-DE", "ja-JP" };
        return Task.FromResult(supportedLanguages);
    }

    /// <summary>
    /// 获取支持的语音选项
    /// </summary>
    public Task<string[]> GetVoiceOptionsAsync()
    {
        return Task.FromResult(OpenAiVoices);
    }
}

/// <summary>
/// Snake case 命名策略，用于 JSON 序列化
/// </summary>
public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return ToSnakeCase(name);
    }

    private static string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var builder = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    builder.Append('_');
                builder.Append(char.ToLower(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}