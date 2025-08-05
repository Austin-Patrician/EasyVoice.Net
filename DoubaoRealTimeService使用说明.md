# DoubaoRealTimeService 使用说明

## 概述

DoubaoRealTimeService 是一个基于豆包（Doubao）实时语音对话API的.NET服务，支持实时语音对话、音频流处理和WebSocket通信。

## 功能特性

- ✅ 实时语音对话
- ✅ WebSocket连接管理
- ✅ 音频录制和播放
- ✅ 会话状态管理
- ✅ 事件驱动架构
- ✅ 错误处理和重连机制
- ✅ 二进制协议支持

## 快速开始

### 1. 服务注册

在 `Program.cs` 中已经注册了服务：

```csharp
// 注册实时语音服务
builder.Services.AddScoped<EasyVoice.Core.Interfaces.IRealTimeService, EasyVoice.Core.Services.DoubaoRealTimeService>();
```

### 2. 基本使用示例

#### 方式一：使用TtsController的快速演示方法

```http
POST /api/tts/realtime-dialog-demo
Content-Type: application/json

{
  "appId": "your_doubao_app_id",
  "accessToken": "your_doubao_access_token",
  "botName": "豆包助手",
  "systemRole": "你是一个友好的AI助手，使用活泼灵动的女声。",
  "greetingMessage": "你好！我是豆包，很高兴为您服务！"
}
```

**响应示例：**
```json
{
  "sessionId": "12345678-1234-1234-1234-123456789abc",
  "status": "connected",
  "message": "实时语音对话会话创建成功",
  "connectionState": "Connected",
  "instructions": {
    "next_steps": [
      "使用 /api/realtime/session/{sessionId}/audio/start-recording 开始录音",
      "使用 /api/realtime/session/{sessionId}/audio/start-playback 开始播放",
      "使用 WebSocket 连接 /api/realtime/session/{sessionId}/websocket 进行实时音频流传输",
      "使用 /api/realtime/session/{sessionId}/finish 结束会话"
    ],
    "audio_format": "PCM, 24kHz, 单声道",
    "websocket_url": "/api/realtime/session/{sessionId}/websocket"
  }
}
```

#### 方式二：使用RealTimeController的完整API

##### 2.1 创建会话

```http
POST /api/realtime/session/create
Content-Type: application/json

{
  "appId": "your_doubao_app_id",
  "accessToken": "your_doubao_access_token",
  "webSocketUrl": "wss://openspeech.bytedance.com/api/v3/realtime/dialogue",
  "connectionTimeoutMs": 30000,
  "audioBufferSeconds": 100
}
```

##### 2.2 开始会话

```http
POST /api/realtime/session/{sessionId}/start
Content-Type: application/json

{
  "botName": "豆包助手",
  "systemRole": "你是一个友好的AI助手，使用活泼灵动的女声。",
  "speakingStyle": "你的说话风格简洁明了，语速适中，语调自然。",
  "audioConfig": {
    "channel": 1,
    "format": "pcm",
    "sampleRate": 24000
  }
}
```

##### 2.3 发送问候语

```http
POST /api/realtime/session/{sessionId}/hello
Content-Type: application/json

{
  "content": "你好！我是豆包，很高兴为您服务！"
}
```

##### 2.4 音频控制

**开始录音：**
```http
POST /api/realtime/session/{sessionId}/audio/start-recording
```

**停止录音：**
```http
POST /api/realtime/session/{sessionId}/audio/stop-recording
```

**开始播放：**
```http
POST /api/realtime/session/{sessionId}/audio/start-playback
```

**停止播放：**
```http
POST /api/realtime/session/{sessionId}/audio/stop-playback
```

##### 2.5 获取会话状态

```http
GET /api/realtime/session/{sessionId}/status
```

##### 2.6 WebSocket连接

```javascript
// JavaScript WebSocket 连接示例
const ws = new WebSocket(`ws://localhost:5000/api/realtime/session/${sessionId}/websocket`);

ws.onopen = function(event) {
    console.log('WebSocket连接已建立');
};

ws.onmessage = function(event) {
    // 处理接收到的音频数据
    console.log('接收到数据:', event.data);
};

ws.onerror = function(error) {
    console.error('WebSocket错误:', error);
};

// 发送音频数据
function sendAudioData(audioBuffer) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(audioBuffer);
    }
}
```

##### 2.7 结束会话

```http
POST /api/realtime/session/{sessionId}/finish
```

## 完整使用流程

### 1. 准备工作

- 获取豆包AppId和AccessToken
- 确保网络连接正常
- 准备音频设备（麦克风和扬声器）

### 2. 建立连接

```csharp
// 在Controller或Service中注入IRealTimeService
public class YourController : ControllerBase
{
    private readonly IRealTimeService _realTimeService;
    
    public YourController(IRealTimeService realTimeService)
    {
        _realTimeService = realTimeService;
    }
    
    public async Task<IActionResult> StartRealTimeDialog()
    {
        // 配置连接
        var config = new RealTimeConnectionConfig
        {
            AppId = "your_app_id",
            AccessToken = "your_access_token",
            WebSocketUrl = "wss://openspeech.bytedance.com/api/v3/realtime/dialogue"
        };
        
        // 设置事件处理
        _realTimeService.ConnectionStateChanged += OnConnectionStateChanged;
        _realTimeService.AudioDataReceived += OnAudioDataReceived;
        _realTimeService.DialogEvent += OnDialogEvent;
        _realTimeService.ErrorOccurred += OnErrorOccurred;
        
        // 连接
        var connected = await _realTimeService.ConnectAsync(config);
        if (!connected)
        {
            return BadRequest("连接失败");
        }
        
        return Ok("连接成功");
    }
}
```

### 3. 事件处理

```csharp
private void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
{
    Console.WriteLine($"连接状态变化: {e.OldState} -> {e.NewState}");
}

private void OnAudioDataReceived(object sender, AudioDataReceivedEventArgs e)
{
    Console.WriteLine($"接收到音频数据: {e.AudioData.Length} 字节");
    // 处理音频数据，例如播放或保存
}

private void OnDialogEvent(object sender, DialogEventArgs e)
{
    Console.WriteLine($"对话事件: {e.EventType}, 内容: {e.Content}");
}

private void OnErrorOccurred(object sender, ErrorEventArgs e)
{
    Console.WriteLine($"发生错误: {e.ErrorMessage}");
    Console.WriteLine($"异常详情: {e.Exception}");
}
```

### 4. 会话管理

```csharp
// 开始会话
var sessionId = Guid.NewGuid().ToString();
var sessionPayload = new StartSessionPayload
{
    Tts = new TtsConfig
    {
        AudioConfig = new AudioConfig
        {
            Channel = 1,
            Format = "pcm",
            SampleRate = 24000
        }
    },
    Dialog = new DialogConfig
    {
        BotName = "豆包",
        SystemRole = "你是一个友好的AI助手",
        SpeakingStyle = "自然流畅"
    }
};

var started = await _realTimeService.StartSessionAsync(sessionId, sessionPayload);
```

### 5. 音频处理

```csharp
// 开始录音
await _realTimeService.StartAudioRecordingAsync(sessionId);

// 发送音频数据
byte[] audioData = GetAudioDataFromMicrophone();
await _realTimeService.SendAudioDataAsync(sessionId, audioData);

// 开始播放
await _realTimeService.StartAudioPlaybackAsync();

// 停止录音
await _realTimeService.StopAudioRecordingAsync();
```

### 6. 清理资源

```csharp
// 结束会话
await _realTimeService.FinishSessionAsync(sessionId);

// 断开连接
await _realTimeService.DisconnectAsync();

// 释放资源
_realTimeService.Dispose();
```

## 配置参数说明

### RealTimeConnectionConfig

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| AppId | string | 是 | - | 豆包应用ID |
| AccessToken | string | 是 | - | 豆包访问令牌 |
| WebSocketUrl | string | 否 | wss://openspeech.bytedance.com/api/v3/realtime/dialogue | WebSocket连接地址 |
| ConnectionTimeoutMs | int | 否 | 30000 | 连接超时时间（毫秒） |
| AudioBufferSeconds | int | 否 | 100 | 音频缓冲区大小（秒） |

### AudioConfig

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| Channel | int | 否 | 1 | 音频通道数 |
| Format | string | 否 | pcm | 音频格式 |
| SampleRate | int | 否 | 24000 | 采样率 |

### DialogConfig

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| BotName | string | 否 | 豆包 | 机器人名称 |
| SystemRole | string | 否 | - | 系统角色设定 |
| SpeakingStyle | string | 否 | - | 说话风格 |

## 错误处理

### 常见错误及解决方案

1. **连接失败**
   - 检查AppId和AccessToken是否正确
   - 确认网络连接正常
   - 验证WebSocket URL是否可访问

2. **音频播放问题**
   - 检查音频设备是否正常
   - 确认音频格式配置正确
   - 验证音频数据是否完整

3. **会话超时**
   - 适当增加ConnectionTimeoutMs值
   - 检查网络稳定性
   - 实现重连机制

### 错误码说明

| 错误码 | 说明 | 解决方案 |
|--------|------|----------|
| 1001 | 连接超时 | 检查网络连接，增加超时时间 |
| 1002 | 认证失败 | 验证AppId和AccessToken |
| 1003 | 会话不存在 | 重新创建会话 |
| 1004 | 音频格式错误 | 检查音频配置参数 |

## 性能优化建议

1. **连接池管理**
   - 复用WebSocket连接
   - 合理设置连接超时时间
   - 实现连接健康检查

2. **音频缓冲**
   - 适当调整音频缓冲区大小
   - 使用异步音频处理
   - 避免音频数据积压

3. **内存管理**
   - 及时释放音频资源
   - 避免内存泄漏
   - 使用对象池减少GC压力

4. **并发控制**
   - 限制同时连接数
   - 使用信号量控制并发
   - 实现优雅降级

## 注意事项

1. **安全性**
   - 不要在客户端暴露AccessToken
   - 使用HTTPS/WSS加密传输
   - 实现访问控制和限流

2. **稳定性**
   - 实现断线重连机制
   - 添加心跳检测
   - 处理网络异常情况

3. **兼容性**
   - 确保音频格式兼容
   - 测试不同设备和浏览器
   - 处理跨平台差异

## 示例项目

完整的示例代码已集成在EasyVoice.Net项目中，包括：

- `RealTimeController.cs` - 完整的REST API实现
- `TtsController.cs` - 快速演示方法
- `DoubaoRealTimeService.cs` - 核心服务实现
- `RealtimeModels.cs` - 数据模型定义

## 技术支持

如果在使用过程中遇到问题，请：

1. 查看日志输出获取详细错误信息
2. 检查网络连接和配置参数
3. 参考本文档的错误处理部分
4. 联系技术支持团队

---

**版本信息：** v1.0.0  
**更新时间：** 2024年12月  
**兼容性：** .NET 9.0+, ASP.NET Core 9.0+