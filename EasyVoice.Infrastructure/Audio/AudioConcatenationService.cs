using EasyVoice.Core.Interfaces;
using FFMpegCore;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyVoice.Infrastructure.Audio;

/// <summary>
/// 音频和字幕合并服务实现 - 模仿 Node.js 版本的逻辑
/// </summary>
public class AudioConcatenationService : IAudioConcatenationService
{
    public async Task<string> ConcatenateAudioFilesAsync(
        List<string> inputFiles, 
        string outputPath, 
        CancellationToken cancellationToken = default)
    {
        if (inputFiles == null || !inputFiles.Any())
            throw new ArgumentException("Input files cannot be null or empty");

        if (inputFiles.Count == 1)
        {
            // 单个文件，直接复制
            File.Copy(inputFiles[0], outputPath, true);
            return outputPath;
        }

        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 按文件名中的数字排序（模仿 Node.js 版本的 sortAudioDir 逻辑）
        var sortedFiles = SortAudioFiles(inputFiles);

        try
        {
            // 恢复到与 Node.js 版本完全一致的无损合并方案
            // 始终使用 'copy' 编解码器，不进行任何重新编码或滤波
            await FFMpegArguments
                .FromConcatInput(sortedFiles)
                .OutputToFile(outputPath, overwrite: true, options => options
                    .WithAudioCodec("copy") // 关键：始终使用 copy codec
                    .WithCustomArgument("-safe 0")) // 允许相对路径
                .ProcessAsynchronously();

            return outputPath;
        }
        catch (Exception ex)
        {
            // FFmpeg 不可用时，使用简单的字节级合并作为备选方案
            Console.WriteLine($"FFmpeg not available, using fallback method: {ex.Message}");
            
            return await ConcatenateAudioFilesFallback(sortedFiles, outputPath, cancellationToken);
        }
    }

    /// <summary>
    /// 备选音频合并方法（当 FFmpeg 不可用时）
    /// </summary>
    private async Task<string> ConcatenateAudioFilesFallback(
        List<string> sortedFiles, 
        string outputPath, 
        CancellationToken cancellationToken)
    {
        // 简单的二进制合并（适用于相同格式的音频文件）
        using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        
        foreach (var file in sortedFiles)
        {
            if (File.Exists(file))
            {
                var fileBytes = await File.ReadAllBytesAsync(file, cancellationToken);
                await outputStream.WriteAsync(fileBytes, cancellationToken);
            }
        }
        
        Console.WriteLine($"Audio files merged using fallback method: {outputPath}");
        return outputPath;
    }

    public async Task<string> ConcatenateSubtitleFilesAsync(
        List<string> inputFiles, 
        string outputPath, 
        CancellationToken cancellationToken = default)
    {
        if (inputFiles == null || !inputFiles.Any())
            throw new ArgumentException("Input files cannot be null or empty");

        if (inputFiles.Count == 1)
        {
            // 单个文件，直接复制
            File.Copy(inputFiles[0], outputPath, true);
            return outputPath;
        }

        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 按文件名排序
        var sortedFiles = SortAudioFiles(inputFiles, ".srt");

        var mergedSubtitles = new List<SubtitleEntry>();
        var totalDuration = TimeSpan.Zero;

        foreach (var file in sortedFiles)
        {
            if (!File.Exists(file)) continue;

            var subtitles = await ParseSrtFileAsync(file, cancellationToken);
            
            // 调整时间戳，加上之前所有片段的总时长
            foreach (var subtitle in subtitles)
            {
                subtitle.StartTime = subtitle.StartTime.Add(totalDuration);
                subtitle.EndTime = subtitle.EndTime.Add(totalDuration);
                mergedSubtitles.Add(subtitle);
            }

            // 更新总时长（使用最后一个字幕的结束时间）
            if (subtitles.Any())
            {
                var lastSubtitle = subtitles.Last();
                totalDuration = lastSubtitle.EndTime;
            }
        }

        // 写入合并后的字幕文件
        await WriteSrtFileAsync(outputPath, mergedSubtitles, cancellationToken);

        return outputPath;
    }

    public async Task<(string AudioPath, string SubtitlePath)> ConcatenateBothAsync(
        List<string> audioFiles,
        List<string> subtitleFiles,
        string outputAudioPath,
        string outputSubtitlePath,
        CancellationToken cancellationToken = default)
    {
        // 并行合并音频和字幕
        var audioTask = ConcatenateAudioFilesAsync(audioFiles, outputAudioPath, cancellationToken);
        var subtitleTask = ConcatenateSubtitleFilesAsync(subtitleFiles, outputSubtitlePath, cancellationToken);

        await Task.WhenAll(audioTask, subtitleTask);

        return (await audioTask, await subtitleTask);
    }

    /// <summary>
    /// 按文件名中的数字排序文件（模仿 Node.js 版本）
    /// </summary>
    private static List<string> SortAudioFiles(List<string> files, string extension = ".mp3")
    {
        return files
            .Where(file => Path.GetExtension(file).Equals(extension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file =>
            {
                // 提取文件名中的数字进行排序（如 1_splits.mp3, 2_splits.mp3）
                var fileName = Path.GetFileNameWithoutExtension(file);
                var match = Regex.Match(fileName, @"(\d+)");
                return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
            })
            .ToList();
    }

    /// <summary>
    /// 解析 SRT 字幕文件
    /// </summary>
    private static async Task<List<SubtitleEntry>> ParseSrtFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var subtitles = new List<SubtitleEntry>();
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        
        var blocks = content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3) continue;

            // 解析时间戳行
            if (lines.Length >= 2 && lines[1].Contains("-->"))
            {
                var timeParts = lines[1].Split("-->", StringSplitOptions.RemoveEmptyEntries);
                if (timeParts.Length == 2)
                {
                    if (TryParseTimeSpan(timeParts[0].Trim(), out var startTime) &&
                        TryParseTimeSpan(timeParts[1].Trim(), out var endTime))
                    {
                        var text = string.Join("\n", lines.Skip(2));
                        subtitles.Add(new SubtitleEntry
                        {
                            StartTime = startTime,
                            EndTime = endTime,
                            Text = text
                        });
                    }
                }
            }
        }

        return subtitles;
    }

    /// <summary>
    /// 写入 SRT 字幕文件
    /// </summary>
    private static async Task WriteSrtFileAsync(string filePath, List<SubtitleEntry> subtitles, CancellationToken cancellationToken)
    {
        var content = new StringBuilder();
        
        for (int i = 0; i < subtitles.Count; i++)
        {
            var subtitle = subtitles[i];
            content.AppendLine($"{i + 1}");
            content.AppendLine($"{FormatTimeSpan(subtitle.StartTime)} --> {FormatTimeSpan(subtitle.EndTime)}");
            content.AppendLine(subtitle.Text);
            content.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, content.ToString(), cancellationToken);
    }

    /// <summary>
    /// 解析时间戳字符串
    /// </summary>
    private static bool TryParseTimeSpan(string timeString, out TimeSpan timeSpan)
    {
        timeSpan = TimeSpan.Zero;
        
        // SRT 格式：00:00:00,000
        var match = Regex.Match(timeString, @"(\d{2}):(\d{2}):(\d{2}),(\d{3})");
        if (match.Success)
        {
            var hours = int.Parse(match.Groups[1].Value);
            var minutes = int.Parse(match.Groups[2].Value);
            var seconds = int.Parse(match.Groups[3].Value);
            var milliseconds = int.Parse(match.Groups[4].Value);
            
            timeSpan = new TimeSpan(0, hours, minutes, seconds, milliseconds);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 格式化时间戳为 SRT 格式
    /// </summary>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        return $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2},{timeSpan.Milliseconds:D3}";
    }
}

/// <summary>
/// 字幕条目
/// </summary>
public class SubtitleEntry
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = string.Empty;
}
