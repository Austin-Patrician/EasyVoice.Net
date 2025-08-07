using System.Collections.Concurrent;
using NAudio.Wave;

namespace EasyVoice.RealtimeDialog
{
    /// <summary>
    /// 音频配置数据类
    /// </summary>
    public class AudioConfigData
    {
        public string Format { get; set; } = string.Empty;
        public int BitSize { get; set; }
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public int Chunk { get; set; }
    }

    /// <summary>
    /// 音频设备管理类，处理音频输入输出
    /// </summary>
    public class AudioDeviceManager : IDisposable
    {
        private readonly AudioConfigData _inputConfig;
        private readonly AudioConfigData _outputConfig;
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _waveProvider;
        private bool _disposed = false;

        public AudioDeviceManager(AudioConfigData inputConfig, AudioConfigData outputConfig)
        {
            _inputConfig = inputConfig ?? throw new ArgumentNullException(nameof(inputConfig));
            _outputConfig = outputConfig ?? throw new ArgumentNullException(nameof(outputConfig));
        }

        /// <summary>
        /// 打开音频输入流
        /// </summary>
        public WaveInEvent OpenInputStream()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(_inputConfig.SampleRate, _inputConfig.BitSize, _inputConfig.Channels),
                BufferMilliseconds = (_inputConfig.Chunk * 1000) / _inputConfig.SampleRate
            };
            return _waveIn;
        }

        /// <summary>
        /// 打开音频输出流
        /// </summary>
        public (WaveOutEvent waveOut, BufferedWaveProvider waveProvider) OpenOutputStream()
        {
            var waveFormat = new WaveFormat(_outputConfig.SampleRate, _outputConfig.BitSize, _outputConfig.Channels);
            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(10), // 10秒缓冲
                DiscardOnBufferOverflow = true
            };
            
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_waveProvider);
            
            return (_waveOut, _waveProvider);
        }

        /// <summary>
        /// 清理音频设备资源
        /// </summary>
        public void Cleanup()
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveProvider?.ClearBuffer();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Cleanup();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 豆包音频管理器 - 对话会话管理类
    /// </summary>
    public class DoubaoAudioManager : IDisposable
    {
        private readonly Dictionary<string, object> _wsConfig;
        private readonly string _sessionId;
        private readonly DouBaoRealTimeClient _client;
        private readonly AudioDeviceManager _audioDevice;
        
        // 添加事件支持
        public event Func<byte[], Task>? OnAudioDataReceived;
        public event Func<string, object, Task>? OnDialogEvent;
        
        private bool _isRunning = true;
        private bool _isSessionFinished = false;
        private bool _isUserQuerying = false;
        private bool _isSendingChatTtsText = false;
        private readonly List<byte> _audioBuffer = new();
        
        private readonly ConcurrentQueue<byte[]> _audioQueue = new();
        private WaveOutEvent? _outputStream;
        private BufferedWaveProvider? _waveProvider;
        private bool _isRecording = true;
        private bool _isPlaying = true;
        private readonly Thread _playerThread;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _disposed = false;

        public DoubaoAudioManager(Dictionary<string, object> wsConfig, AudioConfigData inputConfig, AudioConfigData outputConfig)
        {
            _wsConfig = wsConfig ?? throw new ArgumentNullException(nameof(wsConfig));
            _sessionId = Guid.NewGuid().ToString();
            _client = new DouBaoRealTimeClient(_wsConfig, _sessionId);
            _audioDevice = new AudioDeviceManager(inputConfig, outputConfig);
            
            // 设置信号处理
            Console.CancelKeyPress += KeyboardSignalHandler;
            
            // 初始化音频输出流
            (_outputStream, _waveProvider) = _audioDevice.OpenOutputStream();
            _outputStream.Play();
            
            // 启动播放线程
            _playerThread = new Thread(AudioPlayerThread)
            {
                IsBackground = true
            };
            _playerThread.Start();
        }

        /// <summary>
        /// 音频播放线程
        /// </summary>
        private void AudioPlayerThread()
        {
            while (_isPlaying && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_audioQueue.TryDequeue(out var audioData) && audioData != null)
                    {
                        _waveProvider?.AddSamples(audioData, 0, audioData.Length);
                    }
                    else
                    {
                        // 队列为空时等待一小段时间
                        Thread.Sleep(100);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"音频播放错误: {e.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// 发送音频数据
        /// </summary>
        public async Task SendAudioAsync(byte[] audioData)
        {
            await _client.TaskRequestAsync(audioData, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 发送ChatTTS文本
        /// </summary>
        public async Task SendChatTtsTextAsync(string text)
        {
            _isSendingChatTtsText = true;
            await _client.ChatTtsTextAsync(
                _isUserQuerying,
                start: true,
                end: true,
                text,
                _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 处理服务器响应
        /// </summary>
        public void HandleServerResponse(Dictionary<string, object> response)
        {
            if (response == null || response.Count == 0)
                return;

            var messageType = response.GetValueOrDefault("message_type")?.ToString();
            
            if (messageType == "SERVER_ACK" && response.GetValueOrDefault("payload_msg") is byte[] audioData)
            {
                if (_isSendingChatTtsText)
                    return;
                    
                // 触发音频数据事件
                OnAudioDataReceived?.Invoke(audioData);
                
                _audioQueue.Enqueue(audioData);
                _audioBuffer.AddRange(audioData);
            }
            else if (messageType == "SERVER_FULL_RESPONSE")
            {
                Console.WriteLine($"服务器响应: {System.Text.Json.JsonSerializer.Serialize(response)}");
                
                if (response.TryGetValue("event", out var eventObj) && eventObj is int eventCode)
                {
                    var payloadMsg = response.GetValueOrDefault("payload_msg") as Dictionary<string, object>;
                    
                    if (eventCode == 450)
                    {
                        Console.WriteLine($"清空缓存音频: {response.GetValueOrDefault("session_id")}");
                        // 清空音频队列
                        while (_audioQueue.TryDequeue(out _)) { }
                        _isUserQuerying = true;
                        
                        // 触发ASR信息事件
                        OnDialogEvent?.Invoke("ASR_INFO", response);
                    }
                    
                    if (eventCode == 350 && _isSendingChatTtsText && 
                        payloadMsg?.GetValueOrDefault("tts_type")?.ToString() == "chat_tts_text")
                    {
                        // 清空音频队列
                        while (_audioQueue.TryDequeue(out _)) { }
                        _isSendingChatTtsText = false;
                    }
                    
                    if (eventCode == 460)
                    {
                        _isSendingChatTtsText = false;
                        
                        // 触发TTS结束事件
                        OnDialogEvent?.Invoke("TTS_ENDED", response);
                    }
                    
                    if (eventCode == 461)
                    {
                        // 触发TTS响应事件
                        OnDialogEvent?.Invoke("TTS_RESPONSE", response);
                    }
                    
                    if (eventCode == 451)
                    {
                        // 触发ASR识别结果事件
                        OnDialogEvent?.Invoke("ASR_RESPONSE", response);
                    }
                    
                    if (eventCode == 459)
                    {
                        _isUserQuerying = false;
                        
                        // 触发ASR结束事件
                        OnDialogEvent?.Invoke("ASR_ENDED", response);
                        
                        // 概率触发ChatTTSText
                        if (new Random().Next(0, 2) == 0)
                        {
                            _isSendingChatTtsText = true;
                            _ = Task.Run(TriggerChatTtsTextAsync);
                        }
                    }
                }
            }
            else if (messageType == "SERVER_ERROR")
            {
                var errorMsg = response.GetValueOrDefault("payload_msg")?.ToString();
                Console.WriteLine($"服务器错误: {errorMsg}");
                throw new Exception("服务器错误");
            }
        }

        /// <summary>
        /// 概率触发发送ChatTTSText请求
        /// </summary>
        private async Task TriggerChatTtsTextAsync()
        {
            try
            {
                Console.WriteLine("hit ChatTTSText event, start sending...");
                
                await _client.ChatTtsTextAsync(
                    _isUserQuerying,
                    start: true,
                    end: false,
                    "这是第一轮TTS的开始和中间包事件，这两个合而为一了。",
                    _cancellationTokenSource.Token);
                    
                await _client.ChatTtsTextAsync(
                    _isUserQuerying,
                    start: false,
                    end: true,
                    "这是第一轮TTS的结束事件。",
                    _cancellationTokenSource.Token);
                    
                await Task.Delay(10000, _cancellationTokenSource.Token);
                
                await _client.ChatTtsTextAsync(
                    _isUserQuerying,
                    start: true,
                    end: false,
                    "这是第二轮TTS的开始和中间包事件，这两个合而为一了。",
                    _cancellationTokenSource.Token);
                    
                await _client.ChatTtsTextAsync(
                    _isUserQuerying,
                    start: false,
                    end: true,
                    "这是第二轮TTS的结束事件。",
                    _cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ChatTTSText发送错误: {e.Message}");
            }
        }

        /// <summary>
        /// 键盘信号处理
        /// </summary>
        private void KeyboardSignalHandler(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("receive keyboard Ctrl+C");
            _isRecording = false;
            _isPlaying = false;
            _isRunning = false;
            e.Cancel = true; // 阻止程序立即退出
        }

        /// <summary>
        /// 接收循环
        /// </summary>
        public async Task ReceiveLoopAsync()
        {
            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var response = await _client.ReceiveServerResponseAsync(_cancellationTokenSource.Token);
                    HandleServerResponse(response);
                    
                    if (response.TryGetValue("event", out var eventObj) && eventObj is int eventCode &&
                        (eventCode == 152 || eventCode == 153))
                    {
                        Console.WriteLine($"receive session finished event: {eventCode}");
                        _isSessionFinished = true;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("接收任务已取消");
            }
            catch (Exception e)
            {
                Console.WriteLine($"接收消息错误: {e.Message}");
            }
        }

        /// <summary>
        /// 处理麦克风输入
        /// </summary>
        public async Task ProcessMicrophoneInputAsync()
        {
            await _client.SayHelloAsync(_cancellationTokenSource.Token);
            
            var waveIn = _audioDevice.OpenInputStream();
            Console.WriteLine("已打开麦克风，请讲话...");
            
            waveIn.DataAvailable += async (sender, e) =>
            {
                if (_isRecording && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var audioData = new byte[e.BytesRecorded];
                        Array.Copy(e.Buffer, audioData, e.BytesRecorded);
                        
                        SavePcmToWav(audioData, "input.wav");
                        await _client.TaskRequestAsync(audioData, _cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"读取麦克风数据出错: {ex.Message}");
                    }
                }
            };
            
            waveIn.StartRecording();
            
            // 等待录音结束
            while (_isRecording && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
            
            waveIn.StopRecording();
        }

        /// <summary>
        /// 启动对话会话
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                await _client.ConnectAsync(_cancellationTokenSource.Token);
                
                var microphoneTask = ProcessMicrophoneInputAsync();
                var receiveTask = ReceiveLoopAsync();
                
                // 等待任一任务完成或取消
                await Task.WhenAny(microphoneTask, receiveTask);
                
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
                
                await _client.FinishSessionAsync(_cancellationTokenSource.Token);
                
                while (!_isSessionFinished && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
                
                await _client.FinishConnectionAsync(_cancellationTokenSource.Token);
                await Task.Delay(100, _cancellationTokenSource.Token);
                await _client.CloseAsync(_cancellationTokenSource.Token);
                
                Console.WriteLine($"dialog request completed");
                SaveAudioToPcmFile(_audioBuffer.ToArray(), "output.pcm");
            }
            catch (Exception e)
            {
                Console.WriteLine($"会话错误: {e.Message}");
            }
            finally
            {
                _audioDevice.Cleanup();
            }
        }

        /// <summary>
        /// 保存PCM数据为WAV文件
        /// </summary>
        private static void SavePcmToWav(byte[] pcmData, string filename)
        {
            try
            {
                using var writer = new WaveFileWriter(filename, new WaveFormat(16000, 16, 1));
                writer.Write(pcmData, 0, pcmData.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine($"保存WAV文件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 保存原始PCM音频数据到文件
        /// </summary>
        private static void SaveAudioToPcmFile(byte[] audioData, string filename)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Console.WriteLine("No audio data to save.");
                return;
            }
            
            try
            {
                File.WriteAllBytes(filename, audioData);
                Console.WriteLine($"音频数据已保存到: {filename}");
            }
            catch (IOException e)
            {
                Console.WriteLine($"Failed to save pcm file: {e.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _isRunning = false;
                _isRecording = false;
                _isPlaying = false;
                
                _cancellationTokenSource.Cancel();
                
                // 等待播放线程结束
                if (_playerThread.IsAlive)
                {
                    _playerThread.Join(TimeSpan.FromSeconds(2));
                }
                
                _audioDevice?.Dispose();
                _outputStream?.Dispose();
                _client?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Console.CancelKeyPress -= KeyboardSignalHandler;
                _disposed = true;
            }
        }
    }
}