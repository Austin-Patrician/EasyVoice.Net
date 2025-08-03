using System.Globalization;
using EasyVoice.Core.Constants;
using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Interfaces.Tts;
using EasyVoice.Core.Models;
using Microsoft.Extensions.Logging;

namespace EasyVoice.Core.Services;

/// <summary>
/// 增强的 LLM 服务实现
/// 集成智能选择、并发处理、缓存优化等功能
/// </summary>
public class LlmService : ILlmService
{
    private readonly ILogger<LlmService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<TtsEngineType, EngineUsageStats> _usageStats;
    private readonly Lock _statsLock = new();
    private readonly ITtsEngineFactory _ttsEngineFactory;

    /// <summary>
    /// LlmService 实例
    /// </summary>
    public LlmService(
        ITtsEngineFactory ttsEngineFactory,
        HttpClient? httpClient = null,
        ILogger<LlmService>? logger = null)
    {
        _ttsEngineFactory = ttsEngineFactory;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        _usageStats = new Dictionary<TtsEngineType, EngineUsageStats>();
        // 初始化HTTP客户端
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EasyVoice.Net/2.0");
    }


    /// <summary>
    /// 使用 OpenAI TTS 生成语音
    /// </summary>
    public async Task<TtsResponse> GenerateWithOpenAiAsync(TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await GenerateWithEngineAsync(request, TtsEngineType.OpenAi, cancellationToken);
    }

    /// <summary>
    /// 使用 OpenAI TTS 生成语音
    /// </summary>
    public async Task<Stream> GenerateWithOpenAiStreamAsync(TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await GenerateWithEngineStreamAsync(request, TtsEngineType.OpenAi, cancellationToken);
    }

    /// <summary>
    /// 使用豆包 TTS 生成语音
    /// </summary>
    public async Task<TtsResponse> GenerateWithDoubaoAsync(TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await GenerateWithEngineAsync(request, TtsEngineType.Doubao, cancellationToken);
    }


    /// <summary>
    /// 使用豆包 TTS 生成语音
    /// </summary>
    public async Task<Stream> GenerateWithDoubaoStreamAsync(TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await GenerateWithEngineStreamAsync(request, TtsEngineType.Doubao, cancellationToken);
    }

    /// <summary>
    /// 使用 Kokoro TTS 生成语音
    /// </summary>
    public async Task<TtsResponse> GenerateWithKokoroAsync(TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await GenerateWithEngineAsync(request, TtsEngineType.Kokoro, cancellationToken);
    }


    /// <summary>
    /// 获取支持的 TTS 引擎列表
    /// </summary>
    public IEnumerable<TtsEngineType> GetSupportedEngines()
    {
        return [TtsEngineType.OpenAi, TtsEngineType.Doubao, TtsEngineType.Kokoro, TtsEngineType.Edge];
    }


    /// <summary>
    /// 通用引擎生成方法
    /// </summary>
    private async Task<TtsResponse> GenerateWithEngineAsync(TtsRequest request, TtsEngineType engineType,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger?.LogInformation("Starting {Engine} TTS generation for text: {TextLength} characters", engineType,
                request.Text.Length);

            // 验证请求参数
            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                return TtsResponseExtensions.CreateError(errorMessage!, engineType, request.RequestId);
            }

            var response = new TtsResponse();

            var executeEngineName = GetExecuteEngineName(engineType);

            var ttsEngine = _ttsEngineFactory.GetEngine(executeEngineName);

            //先检查路径是否存在，没有就先设定默认路径
            CheckAndSetDefaultPath(request.OutputPath);

            var actionResult = await ttsEngine.SynthesizeAsync(
                new TtsEngineRequest(request.Text, request.Voice, request.Speed.ToString(CultureInfo.InvariantCulture),
                    request.Pitch, request.Volume,
                    request.OutputPath!), cancellationToken);

            var endTime = DateTime.UtcNow;
            response.Engine = TtsEngineType.OpenAi;
            response.StartTime = startTime;
            response.EndTime = endTime;
            response.FilePath = actionResult.AudioFilePath;
            response.Success = true;

            _logger?.LogInformation("{Engine} TTS generation completed successfully in {ProcessingTime}ms", engineType,
                response.ProcessingTimeMs);
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating TTS with {Engine}: {ErrorMessage}", engineType, ex.Message);

            await UpdateUsageStatsAsync(engineType, false, 0, request.Text.Length);

            var errorResponse = TtsResponseExtensions.CreateError(ex.Message, engineType, request.RequestId);
            errorResponse.StartTime = startTime;
            errorResponse.EndTime = DateTime.UtcNow;

            return errorResponse;
        }
    }


    private void CheckAndSetDefaultPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            // 设置默认输出路径
            outputPath = Path.Combine(Path.GetTempPath(), "EasyVoice", "TTS");
            Directory.CreateDirectory(outputPath);
        }
    }

    private async Task<Stream> GenerateWithEngineStreamAsync(TtsRequest request, TtsEngineType engineType,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Starting {Engine} TTS generation for text: {TextLength} characters", engineType,
                request.Text.Length);

            // 验证请求参数
            var (isValid, errorMessage) = request.Validate();
            if (!isValid)
            {
                throw new ArgumentException(errorMessage!, nameof(request));
            }

            var executeEngineName = GetExecuteEngineName(engineType);

            var ttsEngine = _ttsEngineFactory.GetEngine(executeEngineName);

            //先检查路径是否存在，没有就先设定默认路径
            CheckAndSetDefaultPath(request.OutputPath);

            var response = await ttsEngine.SynthesizeStreamAsync(
                new TtsEngineRequest(request.Text, request.Voice, request.Speed.ToString(CultureInfo.InvariantCulture),
                    request.Pitch, request.Volume,
                    request.OutputPath), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error generating TTS with {Engine}: {ErrorMessage}", engineType, ex.Message);
            throw;
        }
    }

    private string GetExecuteEngineName(TtsEngineType engineType)
    {
        return engineType switch
        {
            TtsEngineType.OpenAi => "openai",
            TtsEngineType.Doubao => "doubao",
            TtsEngineType.Kokoro => "Kokoro",
            TtsEngineType.Edge => "edge",
            _ => throw new NotSupportedException($"Engine {engineType} not supported")
        };
    }

    /// <summary>
    /// 更新使用统计
    /// </summary>
    private async Task UpdateUsageStatsAsync(TtsEngineType engineType, bool success, long responseTimeMs,
        int textLength)
    {
        await Task.CompletedTask;

        lock (_statsLock)
        {
            if (!_usageStats.TryGetValue(engineType, out var stats))
            {
                stats = new EngineUsageStats { EngineType = engineType };
                _usageStats[engineType] = stats;
            }

            stats.TotalRequests++;
            if (success)
            {
                stats.SuccessfulRequests++;
            }
            else
            {
                stats.FailedRequests++;
            }

            stats.TotalCharactersProcessed += textLength;
            stats.AverageResponseTimeMs = (stats.AverageResponseTimeMs * (stats.TotalRequests - 1) + responseTimeMs) /
                                          stats.TotalRequests;
            stats.LastUsedAt = DateTime.UtcNow;
        }
    }

    #region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #endregion
}