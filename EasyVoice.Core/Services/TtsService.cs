using EasyVoice.Core.Interfaces;
using EasyVoice.Core.Interfaces.Tts;
using EasyVoice.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EasyVoice.Core.Services;

/// <summary>
/// TTS 服务 - 完全复刻 Node.js 版本的实现
/// </summary>
public class TtsService : ITtsService
{
    private readonly IAudioCacheService _audioCache;
    private readonly ITextService _textService;
    private readonly ITtsEngineFactory _ttsEngineFactory;
    private readonly IAudioConcatenationService _audioConcatenationService;

    // 复刻 Node.js 的配置常量
    private const int EdgeApiLimit = 5; // 对应 EDGE_API_LIMIT
    private const string AudioDirectory = "audio"; // 对应 AUDIO_DIR
    private const string StaticDomain = "/audio"; // 对应 STATIC_DOMAIN

    public TtsService(
        IAudioCacheService audioCache, 
        ITextService textService,
        ITtsEngineFactory ttsEngineFactory,
        IAudioConcatenationService audioConcatenationService)
    {
        _audioCache = audioCache;
        _textService = textService;
        _ttsEngineFactory = ttsEngineFactory;
        _audioConcatenationService = audioConcatenationService;
    }

    /// <summary>
    /// taskManager.generateTaskId 的逻辑
    /// </summary>
    private static string GenerateTaskId(object fields, string prefix = "task", int length = 32)
    {
        using var md5 = MD5.Create();
        
        // 获取所有属性并排序
        var jsonString = JsonSerializer.Serialize(fields, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new Dictionary<string, object>();
        
        foreach (var key in dict.Keys.OrderBy(k => k))
        {
            var value = dict[key];
            if (value == null) continue;
            
            md5.TransformBlock(Encoding.UTF8.GetBytes(key), 0, Encoding.UTF8.GetBytes(key).Length, null, 0);
            
            var valueString = value.ToString();
            if (!string.IsNullOrEmpty(valueString))
            {
                if (valueString.Length > 1000)
                {
                    // 对长文本分块处理
                    for (int i = 0; i < valueString.Length; i += 1000)
                    {
                        var chunk = valueString.Substring(i, Math.Min(1000, valueString.Length - i));
                        var chunkBytes = Encoding.UTF8.GetBytes(chunk);
                        md5.TransformBlock(chunkBytes, 0, chunkBytes.Length, null, 0);
                    }
                }
                else
                {
                    var valueJson = JsonSerializer.Serialize(value);
                    var valueBytes = Encoding.UTF8.GetBytes(valueJson);
                    md5.TransformBlock(valueBytes, 0, valueBytes.Length, null, 0);
                }
            }
        }
        
        md5.TransformFinalBlock(new byte[0], 0, 0);
        var hashValue = Convert.ToHexString(md5.Hash!).ToLowerInvariant();
        
        return $"{prefix}{hashValue[..Math.Min(length, hashValue.Length)]}";
    }

    /// <summary>
    /// 复刻 Node.js generateId 函数
    /// </summary>
    private static string GenerateId(string voice, string text)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var safeText =Guid.NewGuid().ToString();
        return $"{voice}-{safeText}-{now}.mp3";
    }
    

    public async Task<TtsResult> GenerateTtsAsync(EdgeTtsRequest request, CancellationToken cancellationToken = default)
    {
        // 1.缓存检查逻辑
        var cacheKey = GenerateTaskId(new { 
            text = request.Text, 
            pitch = request.Pitch, 
            voice = request.Voice, 
            rate = request.Rate, 
            volume = request.Volume 
        });
        
        var cachedResult = await _audioCache.GetAudioAsync(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            return new TtsResult(cachedResult.AudioUrl, cachedResult.SrtUrl);
        }

        // 2. 创建 Segment 
        var segmentId = GenerateId(request.Voice, request.Text);
        var segment = new { id = segmentId, text = request.Text };

        // 3. 文本分割 
        var splitResult = _textService.SplitText(request.Text);
        
        // 4. 获取 TTS 引擎
        var engine = _ttsEngineFactory.GetEngine("edge");
        
        TtsResult result;
        
        if (splitResult.Length <= 1)
        {
            // 单段处理 - 复刻 buildSegment
            result = await BuildSegment(engine, segment, request, cancellationToken);
        }
        else
        {
            // 多段处理 - 复刻 buildSegmentList
            var buildSegments = splitResult.Segments.Select(segmentText => new
            {
                text = segmentText,
                pitch = request.Pitch,
                voice = request.Voice,
                rate = request.Rate,
                volume = request.Volume
            }).ToList();
            
            result = await BuildSegmentList(engine, segment, buildSegments.Cast<dynamic>().ToList(), cancellationToken);
        }

        // 5. 验证结果并缓存 - 复刻 Node.js 逻辑
        ValidateTtsResult(result, segmentId);
        if (!result.IsPartial)
        {
            var cacheData = new AudioCacheData(
                request.Voice,
                request.Text,
                request.Rate ?? "",
                request.Pitch ?? "",
                request.Volume ?? "",
                result.AudioUrl,
                result.SrtUrl
            );
            await _audioCache.SetAudioAsync(cacheKey, cacheData, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// 复刻 Node.js buildSegment 函数
    /// </summary>
    private async Task<TtsResult> BuildSegment(
        ITtsEngine engine, 
        dynamic segment, 
        EdgeTtsRequest request, 
        CancellationToken cancellationToken,
        string dir = "")
    {
        var segmentId = (string)segment.id;
        var text = (string)segment.text;
        
        var outputPath = Path.Combine(AudioDirectory, dir, Path.GetFileNameWithoutExtension(segmentId));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AudioDirectory);
        
        var engineRequest = new TtsEngineRequest(
            text,
            request.Voice,
            request.Rate,
            request.Pitch,
            request.Volume,
            outputPath
        );

        var engineResult = await engine.SynthesizeAsync(engineRequest, cancellationToken);
        
        // 复刻 Node.js 的 setTimeout(() => handleSrt(output, false), 200) 逻辑
        _ = Task.Delay(200, cancellationToken).ContinueWith(async _ => 
        {
            await HandleSrt(engineResult.AudioFilePath, false);
        }, cancellationToken);
        
        return new TtsResult(
            $"{StaticDomain}/{Path.Join(dir, Path.GetFileName(engineResult.AudioFilePath))}", 
            $"{StaticDomain}/{Path.Join(dir, Path.GetFileName(engineResult.SubtitleFilePath))}"
        );
    }

    /// <summary>
    /// 复刻 Node.js buildSegmentList 函数
    /// </summary>
    private async Task<TtsResult> BuildSegmentList(
        ITtsEngine engine,
        dynamic segment,
        List<dynamic> segments,
        CancellationToken cancellationToken)
    {
        var segmentId = (string)segment.id;
        var length = segments.Count;
        var handledLength = 0;

        if (length == 0)
        {
            throw new Exception($"No segments found for task!");
        }

        var tmpDirName = segmentId.Replace(".mp3", "");
        var tmpDirPath = Path.Combine(AudioDirectory, tmpDirName);
        Directory.CreateDirectory(tmpDirPath);
        
        // 保存 AI 段落信息 - 复刻 Node.js 逻辑
        await File.WriteAllTextAsync(
            Path.Combine(tmpDirPath, "ai-segments.json"),
            JsonSerializer.Serialize(segments, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken
        );

        // 创建并发任务 - 复刻 Node.js 的 tasks.map 逻辑
        var audioFiles = new List<string>();
        var tasks = segments.Select((segmentData, index) => new Func<Task<dynamic>>(async () =>
        {
            var text = (string)segmentData.text;
            var pitch = (string)segmentData.pitch;
            var voice = (string)segmentData.voice;
            var rate = (string)segmentData.rate;
            var volume = (string)segmentData.volume;
            
            var outputName = $"{index + 1}_splits";
            var outputPath = Path.Combine(tmpDirPath, outputName);
            
            // 检查分段缓存 - 复刻 Node.js 逻辑
            var segmentCacheKey = GenerateTaskId(new { text, pitch, voice, rate, volume });
            var cache = await _audioCache.GetAudioAsync(segmentCacheKey, cancellationToken);
            if (cache != null)
            {
                Console.WriteLine($"Cache hit[segments]: {voice} {text[..Math.Min(10, text.Length)]}");
                // 对于缓存的情况，我们需要从缓存的URL获取实际的文件路径
                var cachedFilePath = Path.Combine(AudioDirectory, Path.GetFileName(cache.AudioUrl));
                return new { success = true, value = cache, audioPath = cachedFilePath, index };
            }
            
            // 生成音频
            var engineRequest = new TtsEngineRequest(text, voice, rate, pitch, volume, outputPath);
            var result = await engine.SynthesizeAsync(engineRequest, cancellationToken);
            
            Console.WriteLine($"Cache miss and generate audio: {result.AudioFilePath}, {result.SubtitleFilePath}");
            
            Interlocked.Increment(ref handledLength);
            
            // 缓存分段结果
            var segmentCacheData = new AudioCacheData(voice, text, rate, pitch, volume,
                $"{StaticDomain}/{Path.GetFileName(result.AudioFilePath)}",
                $"{StaticDomain}/{Path.GetFileName(result.SubtitleFilePath)}");
            await _audioCache.SetAudioAsync(segmentCacheKey, segmentCacheData, cancellationToken);
            
            return new { success = true, value = result, audioPath = result.AudioFilePath, index };
        })).ToList();

        // 并发执行任务 - 复刻 runConcurrentTasks
        var results = await RunConcurrentTasks(tasks, EdgeApiLimit);
        
        var partial = false;
        if (results.Any(result => !(bool)result.success))
        {
            Console.WriteLine($"Partial result detected, some splits generated audio failed!");
            partial = true;
        }

        // 合并音频和字幕文件 - 复刻 Node.js 的 concatDirAudio 和 concatDirSrt
        var outputFile = Path.Combine(AudioDirectory, segmentId);
        
        // 从结果中提取音频文件路径并排序
        var audioFilePaths = new List<string>();
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if ((bool)result.success && result.value != null)
            {
                // 从任务结果中提取 audioPath
                var taskResult = (dynamic)result.value;
                if (taskResult.audioPath != null)
                {
                    audioFilePaths.Add((string)taskResult.audioPath);
                }
            }
        }
        
        // 使用 AudioConcatenationService 合并
        await _audioConcatenationService.ConcatenateAudioFilesAsync(audioFilePaths, outputFile, cancellationToken);
        
        // 合并字幕文件
        var subtitleFiles = audioFilePaths.Select(f => f + ".json").ToList();
        var outputSrtFile = outputFile.Replace(".mp3", ".srt");
        await _audioConcatenationService.ConcatenateSubtitleFilesAsync(subtitleFiles, outputSrtFile, cancellationToken);

        Console.WriteLine($"Concatenating SRT files from {tmpDirPath} to {outputSrtFile}");

        return new TtsResult(
            $"{StaticDomain}/{Path.GetFileName(outputFile)}", 
            $"{StaticDomain}/{Path.GetFileName(outputSrtFile)}",
            partial
        );
    }

    /// <summary>
    /// 复刻 Node.js runConcurrentTasks 函数
    /// </summary>
    private static async Task<List<dynamic>> RunConcurrentTasks(List<Func<Task<dynamic>>> tasks, int limit)
    {
        Console.WriteLine($"Running {tasks.Count} tasks with a limit of {limit}");
        
        var controller = new ConcurrencyController(tasks, limit, () =>
            Console.WriteLine("All concurrent tasks completed"));
        
        var (results, cancelled) = await controller.RunAsync();
        Console.WriteLine($"Tasks completed: {results.Count}, cancelled: {cancelled}");
        
        return results;
    }

    /// <summary>
    /// 验证 TTS 结果
    /// </summary>
    private static void ValidateTtsResult(TtsResult result, string segmentId)
    {
        if (string.IsNullOrEmpty(result.AudioUrl))
        {
            throw new Exception($"Incomplete TTS result for segment {segmentId}");
        }
    }

    /// <summary>
    /// 复刻 Node.js handleSrt 函数
    /// </summary>
    private static async Task HandleSrt(string outputPath, bool deleteJson)
    {
        // 这里应该实现 SRT 处理逻辑
        // 暂时留空，因为需要更多上下文
        await Task.CompletedTask;
    }
    

    public async Task<Stream> GenerateTtsStreamAsync(EdgeTtsRequest request, CancellationToken cancellationToken = default)
    {
        // 流式生成保持原有逻辑，因为已经验证工作良好
        var cacheKey = GenerateTaskId(new { 
            text = request.Text, 
            pitch = request.Pitch, 
            voice = request.Voice, 
            rate = request.Rate, 
            volume = request.Volume 
        });
        
        var cachedResult = await _audioCache.GetAudioAsync(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            var audioFilePath = Path.Combine(AudioDirectory, Path.GetFileName(cachedResult.AudioUrl));
            if (File.Exists(audioFilePath))
            {
                return new FileStream(audioFilePath, FileMode.Open, FileAccess.Read);
            }
        }

        var splitResult = _textService.SplitText(request.Text);
        var segmentId = GenerateId(request.Voice, request.Text);
        var engine = _ttsEngineFactory.GetEngine("edge");

        Stream resultStream;

        if (splitResult.Length <= 1)
        {
            resultStream = await GenerateSingleSegmentStream(engine, request, segmentId, cancellationToken);
        }
        else
        {
            resultStream = await GenerateMultipleSegmentsStream(engine, request, splitResult.Segments.ToList(), segmentId, cancellationToken);
        }

        return resultStream;
    }

    private async Task<Stream> GenerateSingleSegmentStream(
        ITtsEngine engine, 
        EdgeTtsRequest request, 
        string segmentId, 
        CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(AudioDirectory, Path.GetFileNameWithoutExtension(segmentId));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AudioDirectory);
        
        var engineRequest = new TtsEngineRequest(
            request.Text,
            request.Voice,
            request.Rate,
            request.Pitch,
            request.Volume,
            outputPath
        );

        return await engine.SynthesizeStreamAsync(engineRequest, cancellationToken);
    }

    private async Task<Stream> GenerateMultipleSegmentsStream(
        ITtsEngine engine,
        EdgeTtsRequest request,
        List<string> segments,
        string segmentId,
        CancellationToken cancellationToken)
    {
        var outputDir = Path.Combine(AudioDirectory, Path.GetFileNameWithoutExtension(segmentId));
        Directory.CreateDirectory(outputDir);

        var audioFiles = new List<string>();
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var segmentOutputPath = Path.Combine(outputDir, $"{i + 1}_splits");
            
            var engineRequest = new TtsEngineRequest(
                segment,
                request.Voice,
                request.Rate,
                request.Pitch,
                request.Volume,
                segmentOutputPath
            );

            var result = await engine.SynthesizeAsync(engineRequest, cancellationToken);
            audioFiles.Add(result.AudioFilePath);
        }

        var finalAudioPath = Path.Combine(AudioDirectory, segmentId);
        var mergedPath = await _audioConcatenationService.ConcatenateAudioFilesAsync(
            audioFiles, finalAudioPath, cancellationToken);

        return new FileStream(mergedPath, FileMode.Open, FileAccess.Read);
    }
}