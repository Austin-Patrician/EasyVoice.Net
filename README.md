# EasyVoice.Net - 豆包实时语音 .NET SDK

一个用于豆包实时语音服务的 .NET 客户端库，提供简单易用的 API 来实现实时语音对话功能。

## 特性

- ✅ 实时语音识别 (ASR)
- ✅ 实时语音合成 (TTS)
- ✅ 智能对话机器人
- ✅ WebSocket 长连接
- ✅ 异步编程模型
- ✅ 完整的错误处理
- ✅ 性能优化 (ArrayPool, ConfigureAwait)

## 快速开始

### 1. 安装

```bash
dotnet add package EasyVoice.RealtimeDialog
```

### 2. 基本使用

```csharp
using EasyVoice.RealtimeDialog;
using EasyVoice.RealtimeDialog.Models;
using Microsoft.Extensions.Logging;

// 创建日志记录器
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<RealtimeClient>();

// 创建客户端
using var client = new RealtimeClient(logger);

// 配置连接参数
var config = new SessionConfig
{
    AppId = "your-app-id",
    AccessKey = "your-access-key",
    BotName = "豆包",
    SystemRole = "你是一个友好的AI助手",
    EnableServerVad = true
};

// 设置事件处理
client.OnTextReceived += async (text) =>
{
    Console.WriteLine($"收到文本: {text}");
};

client.OnAudioReceived += async (audioData) =>
{
    Console.WriteLine($"收到音频: {audioData.Length} 字节");
    // 处理音频数据，例如播放
};

client.OnError += async (error) =>
{
    Console.WriteLine($"发生错误: {error.Message}");
};

// 连接到服务
if (await client.ConnectAsync(config))
{
    Console.WriteLine("连接成功！");
    
    // 发送文本消息
    await client.SendTextAsync("你好，豆包！");
    
    // 发送音频数据
    var audioData = File.ReadAllBytes("audio.pcm");
    await client.SendAudioAsync(audioData);
    
    // 等待一段时间接收响应
    await Task.Delay(5000);
    
    // 断开连接
    await client.DisconnectAsync();
}
```

### 3. 音频格式要求

- **格式**: PCM
- **采样率**: 16000 Hz
- **位深度**: 16 bit
- **声道数**: 1 (单声道)
- **字节序**: Little Endian

### 4. 配置选项

```csharp
var config = new SessionConfig
{
    AppId = "your-app-id",           // 必需：应用ID
    AccessKey = "your-access-key",   // 必需：访问密钥
    BotName = "豆包",                // 可选：机器人名称
    SystemRole = "你是一个AI助手",    // 可选：系统角色设定
    SpeakingStyle = "friendly",      // 可选：说话风格
    Speaker = "zh_female_shuangkuai", // 可选：语音合成说话人
    EnableServerVad = true,          // 可选：启用服务端VAD
    AudioConfig = new AudioConfig    // 可选：音频配置
    {
        SampleRate = 16000,
        Channels = 1,
        BitDepth = 16,
        Format = "pcm"
    }
};
```

## 事件处理

### 文本事件

```csharp
client.OnTextReceived += async (text) =>
{
    Console.WriteLine($"AI回复: {text}");
};
```

### 音频事件

```csharp
client.OnAudioReceived += async (audioData) =>
{
    // 播放音频或保存到文件
    await File.WriteAllBytesAsync("response.pcm", audioData);
};
```

### 会话事件

```csharp
client.OnSessionStarted += async () =>
{
    Console.WriteLine("会话已开始");
};

client.OnSessionEnded += async () =>
{
    Console.WriteLine("会话已结束");
};
```

### 错误处理

```csharp
client.OnError += async (error) =>
{
    Console.WriteLine($"错误类型: {error.GetType().Name}");
    Console.WriteLine($"错误信息: {error.Message}");
    
    // 根据错误类型进行处理
    switch (error)
    {
        case ConnectionException connEx:
            Console.WriteLine("连接错误，尝试重连...");
            break;
        case AudioException audioEx:
            Console.WriteLine("音频错误，检查音频格式...");
            break;
        case SessionException sessEx:
            Console.WriteLine("会话错误，重新开始会话...");
            break;
    }
};
```

## 最佳实践

### 1. 资源管理

```csharp
// 使用 using 语句确保资源正确释放
using var client = new RealtimeClient(logger);

// 或者手动释放
try
{
    // 使用客户端
}
finally
{
    client?.Dispose();
}
```

### 2. 错误重试

```csharp
public async Task<bool> ConnectWithRetry(SessionConfig config, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            if (await client.ConnectAsync(config))
                return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"连接失败 (尝试 {i + 1}/{maxRetries}): {ex.Message}");
            if (i < maxRetries - 1)
                await Task.Delay(1000 * (i + 1)); // 指数退避
        }
    }
    return false;
}
```

### 3. 音频流处理

```csharp
public async Task SendAudioStream(Stream audioStream)
{
    var buffer = new byte[1024]; // 1KB 缓冲区
    int bytesRead;
    
    while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        var audioChunk = new byte[bytesRead];
        Array.Copy(buffer, audioChunk, bytesRead);
        
        await client.SendAudioAsync(audioChunk);
        await Task.Delay(64); // 控制发送频率 (16ms per chunk for 16kHz)
    }
}
```

## 故障排除

### 常见问题

1. **连接失败**
   - 检查 AppId 和 AccessKey 是否正确
   - 确认网络连接正常
   - 检查防火墙设置

2. **音频无响应**
   - 确认音频格式符合要求 (PCM, 16kHz, 16bit, 单声道)
   - 检查音频数据是否为空
   - 确认会话状态为 Active

3. **文本消息失败**
   - 检查文本内容是否为空
   - 确认会话已正确建立
   - 查看日志中的错误信息

### 调试技巧

```csharp
// 启用详细日志
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole()
           .SetMinimumLevel(LogLevel.Debug) // 设置为 Debug 级别
           .AddFilter("EasyVoice", LogLevel.Trace)); // 显示所有 EasyVoice 日志
```

## 许可证

MIT License

## 支持

如有问题或建议，请提交 Issue 或 Pull Request。