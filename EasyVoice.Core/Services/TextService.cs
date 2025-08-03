using EasyVoice.Core.Interfaces;
using System.Text.RegularExpressions;

namespace EasyVoice.Core.Services;

/// <summary>
/// 文本服务 - 恢复到与 Node.js 版本完全一致的分割逻辑
/// </summary>
public class TextService : ITextService
{
    //分词长度
    private const int DefaultTargetLength = 500;

    public TextSplitResult SplitText(string text, int targetLength = DefaultTargetLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < targetLength)
        {
            return new TextSplitResult(1, new[] { text ?? string.Empty }.ToList().AsReadOnly());
        }

        var segments = new List<string>();
        var currentSegment = "";
        
        // 按句子分隔符分割（模仿 Node.js 版本的逻辑）
        var sentences = Regex.Split(text, @"([。！？.!?])");

        for (int i = 0; i < sentences.Length; i += 2)
        {
            var sentence = (sentences[i] ?? "");
            // 安全地获取标点符号部分
            if (i + 1 < sentences.Length)
            {
                sentence += (sentences[i + 1] ?? "");
            }
            
            if (string.IsNullOrWhiteSpace(sentence)) continue;

            if ((currentSegment + sentence).Length <= targetLength)
            {
                currentSegment += sentence;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentSegment))
                {
                    segments.Add(currentSegment.Trim());
                }
                currentSegment = sentence;
            }
        }

        if (!string.IsNullOrEmpty(currentSegment))
        {
            segments.Add(currentSegment.Trim());
        }

        // 这是一个可以进一步优化的地方，但为了快速对齐，先用简单方式
        var finalSegments = new List<string>();
        foreach (var segment in segments)
        {
            if (segment.Length <= targetLength)
            {
                finalSegments.Add(segment);
            }
            else
            {
                // 对过长段落进行字符级分割
                for (int i = 0; i < segment.Length; i += targetLength)
                {
                    var subSegment = segment.Substring(i, Math.Min(targetLength, segment.Length - i));
                    finalSegments.Add(subSegment);
                }
            }
        }

        return new TextSplitResult(finalSegments.Count, finalSegments.AsReadOnly());
    }
}
