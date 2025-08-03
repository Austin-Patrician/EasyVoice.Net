namespace EasyVoice.Core.Interfaces;

/// <summary>
/// 音频和字幕合并服务接口
/// </summary>
public interface IAudioConcatenationService
{
    /// <summary>
    /// 合并多个音频文件为一个 MP3 文件
    /// </summary>
    /// <param name="inputFiles">输入音频文件路径列表（按顺序）</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并后的音频文件路径</returns>
    Task<string> ConcatenateAudioFilesAsync(
        List<string> inputFiles, 
        string outputPath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 合并多个字幕文件为一个 SRT 文件
    /// </summary>
    /// <param name="inputFiles">输入字幕文件路径列表（按顺序）</param>
    /// <param name="outputPath">输出 SRT 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并后的字幕文件路径</returns>
    Task<string> ConcatenateSubtitleFilesAsync(
        List<string> inputFiles, 
        string outputPath, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 同时合并音频和字幕文件
    /// </summary>
    /// <param name="audioFiles">音频文件路径列表</param>
    /// <param name="subtitleFiles">字幕文件路径列表</param>
    /// <param name="outputAudioPath">输出音频文件路径</param>
    /// <param name="outputSubtitlePath">输出字幕文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并结果</returns>
    Task<(string AudioPath, string SubtitlePath)> ConcatenateBothAsync(
        List<string> audioFiles,
        List<string> subtitleFiles,
        string outputAudioPath,
        string outputSubtitlePath,
        CancellationToken cancellationToken = default);
}
