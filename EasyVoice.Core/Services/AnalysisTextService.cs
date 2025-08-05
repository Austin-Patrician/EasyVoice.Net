using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace EasyVoice.Core.Services;

/// <summary>
/// 文本分析服务实现
/// 使用 Microsoft.SemanticKernel 进行AI驱动的文本分析和语音参数推荐
/// </summary>
public class AnalysisTextService : IAnalysisTextService
{
    private readonly ILogger<AnalysisTextService> _logger;

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    private static readonly List<string> SupportedLanguages = new()
    {
        "zh-CN", "en-US", "ja-JP", "ko-KR", "fr-FR", "de-DE", "es-ES",
        "it-IT", "pt-BR", "ru-RU", "ar-SA", "hi-IN", "th-TH", "vi-VN"
    };

    public AnalysisTextService(ILogger<AnalysisTextService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 分析文本并推荐最适合的语音参数
    /// </summary>
    public async Task<TextAnalysisResult> AnalyzeTextAsync(Kernel kernel, TextAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始分析文本，长度: {TextLength}", request.Text.Length);

            // 验证输入
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsValid)
            {
                return new TextAnalysisResult
                {
                    Success = false,
                    ErrorMessage = validationResult.ErrorMessage
                };
            }

            // 构建分析提示词
            var prompt = BuildAnalysisPrompt(request);

            //construct chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            
            // 调用AI进行分析
            var aiResponse = await chatCompletionService.GetChatMessageContentAsync(prompt, cancellationToken: cancellationToken);
            
            // 解析AI响应
            var analysisResult = ParseAiResponse(aiResponse.Content ?? string.Empty, request);

            _logger.LogInformation("文本分析完成，推荐语音: {VoiceType}", analysisResult.Recommendation?.RecommendedVoiceType);

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本分析过程中发生错误");
            return new TextAnalysisResult
            {
                Success = false,
                ErrorMessage = $"分析过程中发生错误: {ex.Message}"
            };
        }
    }

    public Task<TextAnalysisResult> AnalyzeTextAsync(string baseUrl, string modelName, string apiKey, TextAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        // 创建一个新的Kernel实例
        var openAiHttpClientHandler = new OpenAIHttpClientHandler(baseUrl);
        var httpClient = new HttpClient(openAiHttpClientHandler);
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelName,apiKey, httpClient: httpClient).Build();
        
        // 调用分析方法
        return AnalyzeTextAsync(kernel, request, cancellationToken);
    }

    /// <summary>
    /// 批量分析多个文本
    /// </summary>
    public async Task<List<TextAnalysisResult>> AnalyzeTextsAsync(Kernel kernel, List<TextAnalysisRequest> requests, CancellationToken cancellationToken = default)
    {
        var results = new List<TextAnalysisResult>();
        
        foreach (var request in requests)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var result = await AnalyzeTextAsync(kernel,request, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }

    /// <summary>
    /// 通过OpenAi批量分析多个文本
    /// </summary>
    /// <param name="baseUrl"></param>
    /// <param name="modelName"></param>
    /// <param name="apiKey"></param>
    /// <param name="requests"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<TextAnalysisResult>> AnalyzeTextsAsync(string baseUrl, string modelName, string apiKey, List<TextAnalysisRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TextAnalysisResult>();
        // 创建一个新的Kernel实例
        var openAiHttpClientHandler = new OpenAIHttpClientHandler(baseUrl);
        var httpClient = new HttpClient(openAiHttpClientHandler);
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelName,apiKey, httpClient: httpClient).Build();
        
        foreach (var request in requests)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var result = await AnalyzeTextAsync(kernel,request, cancellationToken);
            results.Add(result);
        }
        
        return results;
    }


    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    public Task<List<string>> GetSupportedLanguagesAsync()
    {
        return Task.FromResult(new List<string>(SupportedLanguages));
    }

    /// <summary>
    /// 验证请求参数
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidateRequest(TextAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return (false, "文本内容不能为空");

        if (request.Text.Length > 10000)
            return (false, "文本长度不能超过10000个字符");

        if (request.AvailableVoiceTypes == null || !request.AvailableVoiceTypes.Any())
            return (false, "可用语音类型列表不能为空");

        return (true, null);
    }

    /// <summary>
    /// 构建AI分析提示词
    /// </summary>
    private static string BuildAnalysisPrompt(TextAnalysisRequest request)
    {
        var voiceTypesJson = JsonSerializer.Serialize(request.AvailableVoiceTypes, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var prompt = $@"
你是一个专业的语音合成参数推荐专家。请分析以下文本内容，并从提供的语音类型中推荐最适合的语音参数。

**要分析的文本：**
{request.Text}

**可用的语音类型：**
{voiceTypesJson}

**分析要求：**
1. 分析文本的语言、情感、类型和风格
2. 根据文本内容推荐最适合的语音类型
3. 推荐合适的语速（0.25-4.0，正常为1.0）
4. 推荐合适的音调（-50%到+200%，正常为+0%）
5. 推荐合适的音量（0%-100%，正常为50%）
6. 提供推荐理由和置信度（0.0-1.0）

**请严格按照以下JSON格式返回结果：**
{{
  ""detectedLanguage"": ""语言代码（如zh-CN, en-US等）"",
  ""sentiment"": ""情感（positive/negative/neutral）"",
  ""textType"": ""文本类型（narrative/dialogue/formal/casual/technical等）"",
  ""recommendedVoiceType"": ""推荐的语音类型名称"",
  ""recommendedLanguage"": ""推荐的语言代码"",
  ""recommendedSpeed"": 推荐的语速数值,
  ""recommendedPitch"": ""推荐的音调（如+0%, -10%, +20%等）"",
  ""recommendedVolume"": ""推荐的音量（如50%, 60%等）"",
  ""reasoning"": ""推荐理由的详细说明"",
  ""confidence"": 置信度数值,
  ""keywords"": [""关键词1"", ""关键词2"", ""关键词3""]
}}

请确保返回的是有效的JSON格式，不要包含任何其他文本。";

        return prompt;
    }

    /// <summary>
    /// 解析AI响应
    /// </summary>
    private TextAnalysisResult ParseAiResponse(string aiResponse, TextAnalysisRequest request)
    {
        try
        {
            // 清理响应文本，移除可能的markdown标记
            var cleanResponse = aiResponse.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }
            cleanResponse = cleanResponse.Trim();

            // 解析JSON响应
            using var document = JsonDocument.Parse(cleanResponse);
            var root = document.RootElement;

            var recommendation = new Models.VoiceRecommendation
            {
                RecommendedVoiceType = GetStringProperty(root, "recommendedVoiceType"),
                RecommendedLanguage = GetStringProperty(root, "recommendedLanguage"),
                RecommendedSpeed = GetFloatProperty(root, "recommendedSpeed", 1.0f),
                RecommendedPitch = GetStringProperty(root, "recommendedPitch", "+0%"),
                RecommendedVolume = GetStringProperty(root, "recommendedVolume", "50%"),
                Reasoning = GetStringProperty(root, "reasoning"),
                Confidence = GetFloatProperty(root, "confidence", 0.8f)
            };

            var analysisDetails = new TextAnalysisDetails
            {
                DetectedLanguage = GetStringProperty(root, "detectedLanguage"),
                Sentiment = GetStringProperty(root, "sentiment"),
                TextType = GetStringProperty(root, "textType"),
                TextLength = request.Text.Length,
                EstimatedReadingTime = CalculateReadingTime(request.Text),
                Keywords = GetStringArrayProperty(root, "keywords")
            };

            return new TextAnalysisResult
            {
                Success = true,
                Recommendation = recommendation,
                AnalysisDetails = analysisDetails
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析AI响应时发生错误: {Response}", aiResponse);
            
            // 返回默认推荐
            return CreateDefaultRecommendation(request);
        }
    }

    /// <summary>
    /// 创建默认推荐（当AI解析失败时使用）
    /// </summary>
    private static TextAnalysisResult CreateDefaultRecommendation(TextAnalysisRequest request)
    {
        var firstVoice = request.AvailableVoiceTypes.FirstOrDefault();
        
        return new TextAnalysisResult
        {
            Success = true,
            Recommendation = new Models.VoiceRecommendation
            {
                RecommendedVoiceType = firstVoice?.Name ?? "default",
                RecommendedLanguage = firstVoice?.Language ?? "zh-CN",
                RecommendedSpeed = 1.0f,
                RecommendedPitch = "+0%",
                RecommendedVolume = "50%",
                Reasoning = "使用默认推荐（AI分析失败）",
                Confidence = 0.5f
            },
            AnalysisDetails = new TextAnalysisDetails
            {
                DetectedLanguage = "zh-CN",
                Sentiment = "neutral",
                TextType = "general",
                TextLength = request.Text.Length,
                EstimatedReadingTime = CalculateReadingTime(request.Text),
                Keywords = new List<string>()
            }
        };
    }

    /// <summary>
    /// 计算预估阅读时间
    /// </summary>
    private static double CalculateReadingTime(string text)
    {
        // 假设平均阅读速度为每分钟200个字符
        const double charactersPerMinute = 200.0;
        return (text.Length / charactersPerMinute) * 60; // 返回秒数
    }

    /// <summary>
    /// 安全获取字符串属性
    /// </summary>
    private static string GetStringProperty(JsonElement element, string propertyName, string defaultValue = "")
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? defaultValue
            : defaultValue;
    }

    /// <summary>
    /// 安全获取浮点数属性
    /// </summary>
    private static float GetFloatProperty(JsonElement element, string propertyName, float defaultValue = 0f)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetSingle(out var value))
                return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// 安全获取字符串数组属性
    /// </summary>
    private static List<string> GetStringArrayProperty(JsonElement element, string propertyName)
    {
        var result = new List<string>();
        
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrEmpty(value))
                        result.Add(value);
                }
            }
        }
        
        return result;
    }
}