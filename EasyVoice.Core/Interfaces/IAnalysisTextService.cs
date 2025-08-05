using EasyVoice.Core.Models;
using Microsoft.SemanticKernel;

namespace EasyVoice.Core.Interfaces;

/// <summary>
/// 文本分析服务接口
/// 提供基于AI的文本分析和语音参数推荐功能
/// </summary>
public interface IAnalysisTextService
{
    /// <summary>
    /// 分析文本并推荐最适合的语音参数
    /// </summary>
    /// <param name="kernel"></param>
    /// <param name="request">文本分析请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分析结果，包含推荐的语音参数</returns>
    Task<TextAnalysisResult> AnalyzeTextAsync(Kernel kernel,TextAnalysisRequest request, CancellationToken cancellationToken = default);

    
    /// <summary>
    /// 分析文本并推荐最适合的语音参数
    /// </summary>
    /// <param name="baseUrl"></param>
    /// <param name="modelName"></param>
    /// <param name="apiKey"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TextAnalysisResult> AnalyzeTextAsync(string baseUrl,string modelName, string apiKey, TextAnalysisRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量分析多个文本
    /// </summary>
    /// <param name="kernel"></param>
    /// <param name="requests">文本分析请求列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分析结果列表</returns>
    Task<List<TextAnalysisResult>> AnalyzeTextsAsync(Kernel kernel,List<TextAnalysisRequest> requests, CancellationToken cancellationToken = default);


    /// <summary>
    /// 批量分析多个文本
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="requests">文本分析请求列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="baseUrl"></param>
    /// <param name="modelName"></param>
    /// <returns>分析结果列表</returns>
    Task<List<TextAnalysisResult>> AnalyzeTextsAsync(string baseUrl,string modelName, string apiKey,List<TextAnalysisRequest> requests, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    /// <returns>支持的语言代码列表</returns>
    Task<List<string>> GetSupportedLanguagesAsync();
}