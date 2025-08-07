using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;


namespace EasyVoice.RealtimeDialog
{
    /// <summary>
    /// 豆包实时语音客户端
    /// </summary>
    public class DouBaoRealTimeClient
    {
        private readonly Dictionary<string, object> _config;
        private readonly string _sessionId;
        private ClientWebSocket? _webSocket;

        public DouBaoRealTimeClient(Dictionary<string, object> config, string sessionId)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        }

        /// <summary>
        /// 建立WebSocket连接
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            var baseUrl = _config["base_url"]?.ToString() ??
                          throw new InvalidOperationException("base_url is required");
            var headers = _config["headers"] as Dictionary<string, string>;
            
            _webSocket = new ClientWebSocket();

            // 添加请求头
            _webSocket.Options.SetRequestHeader("X-Api-App-ID", "7482136989");
            _webSocket.Options.SetRequestHeader("X-Api-Access-Key", "4akGrrTRlikgCCxBVSi0f3gXQ2uGR8bt");
            _webSocket.Options.SetRequestHeader("X-Api-Resource-Id", "volc.speech.dialog");
            _webSocket.Options.SetRequestHeader("X-Api-App-Key", "PlgvMymc7f3tQnJ6");
            _webSocket.Options.SetRequestHeader("X-Api-Connect-Id", Guid.NewGuid().ToString());

            await _webSocket.ConnectAsync(new Uri(baseUrl), cancellationToken);

            // StartConnection request
            await SendStartConnectionAsync(cancellationToken);

            // StartSession request  
            await SendStartSessionAsync(cancellationToken);
        }

        /// <summary>
        /// 发送StartConnection请求
        /// </summary>
        private async Task SendStartConnectionAsync(CancellationToken cancellationToken)
        {
            var startConnectionRequest = new List<byte>();
            startConnectionRequest.AddRange(DoubaoProtocol.GenerateHeader());
            startConnectionRequest.AddRange(BitConverter.GetBytes(1).ReverseIfLittleEndian());

            var payloadBytes = Encoding.UTF8.GetBytes("{}");
            payloadBytes = CompressGzip(payloadBytes);
            startConnectionRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            startConnectionRequest.AddRange(payloadBytes);

            await SendAsync(startConnectionRequest.ToArray(), cancellationToken);
            var response = await ReceiveAsync(cancellationToken);
            var parsedResponse = DoubaoProtocol.ParseResponse(response);
            Console.WriteLine($"StartConnection response: {JsonSerializer.Serialize(parsedResponse)}");
        }

        /// <summary>
        /// 发送StartSession请求
        /// </summary>
        private async Task SendStartSessionAsync(CancellationToken cancellationToken)
        {
            // 这里需要从config中获取start_session_req参数
            var requestParams = new
            {
                tts = new
                {
                    audio_config = new
                    {
                        channel = 1,
                        format = "pcm",
                        sample_rate = 24000,
                    },
                },
                dialog = new
                {
                    bot_name = "豆包",
                    system_role = "你使用活泼灵动的女声，性格开朗，热爱生活。",
                    speaking_style = "你的说话风格简洁明了，语速适中，语调自然。",
                    extra = new
                    {
                        strict_audit = false,
                        audit_response = "支持客户自定义安全审核回复话术。"
                    }
                }
            };

            var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestParams));
            payloadBytes = CompressGzip(payloadBytes);

            var startSessionRequest = new List<byte>();
            startSessionRequest.AddRange(DoubaoProtocol.GenerateHeader());
            startSessionRequest.AddRange(BitConverter.GetBytes(100).ReverseIfLittleEndian());
            startSessionRequest.AddRange(BitConverter.GetBytes(_sessionId.Length).ReverseIfLittleEndian());
            startSessionRequest.AddRange(Encoding.UTF8.GetBytes(_sessionId));
            startSessionRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            startSessionRequest.AddRange(payloadBytes);

            await SendAsync(startSessionRequest.ToArray(), cancellationToken);
            var response = await ReceiveAsync(cancellationToken);
            var parsedResponse = DoubaoProtocol.ParseResponse(response);
            Console.WriteLine($"StartSession response: {JsonSerializer.Serialize(parsedResponse)}");
        }

        /// <summary>
        /// 发送Hello消息
        /// </summary>
        public async Task SayHelloAsync(CancellationToken cancellationToken = default)
        {
            var payload = new Dictionary<string, object>
            {
                ["content"] = "你好，我是豆包，有什么可以帮助你的？"
            };

            var helloRequest = new List<byte>();
            helloRequest.AddRange(DoubaoProtocol.GenerateHeader());
            helloRequest.AddRange(BitConverter.GetBytes(300).ReverseIfLittleEndian());

            var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            payloadBytes = CompressGzip(payloadBytes);

            helloRequest.AddRange(BitConverter.GetBytes(_sessionId.Length).ReverseIfLittleEndian());
            helloRequest.AddRange(Encoding.UTF8.GetBytes(_sessionId));
            helloRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            helloRequest.AddRange(payloadBytes);

            await SendAsync(helloRequest.ToArray(), cancellationToken);
        }

        /// <summary>
        /// 发送Chat TTS Text消息
        /// </summary>
        public async Task ChatTtsTextAsync(bool isUserQuerying, bool start, bool end, string content,
            CancellationToken cancellationToken = default)
        {
            if (isUserQuerying)
            {
                return;
            }

            var payload = new Dictionary<string, object>
            {
                ["start"] = start,
                ["end"] = end,
                ["content"] = content
            };

            Console.WriteLine($"ChatTTSTextRequest payload: {JsonSerializer.Serialize(payload)}");

            var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            payloadBytes = CompressGzip(payloadBytes);

            var chatTtsTextRequest = new List<byte>();
            chatTtsTextRequest.AddRange(DoubaoProtocol.GenerateHeader());
            chatTtsTextRequest.AddRange(BitConverter.GetBytes(500).ReverseIfLittleEndian());
            chatTtsTextRequest.AddRange(BitConverter.GetBytes(_sessionId.Length).ReverseIfLittleEndian());
            chatTtsTextRequest.AddRange(Encoding.UTF8.GetBytes(_sessionId));
            chatTtsTextRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            chatTtsTextRequest.AddRange(payloadBytes);

            await SendAsync(chatTtsTextRequest.ToArray(), cancellationToken);
        }

        /// <summary>
        /// 发送任务请求（音频数据）
        /// </summary>
        public async Task TaskRequestAsync(byte[] audio, CancellationToken cancellationToken = default)
        {
            var taskRequest = new List<byte>();
            taskRequest.AddRange(DoubaoProtocol.GenerateHeader(
                messageType: DoubaoProtocol.CLIENT_AUDIO_ONLY_REQUEST,
                serialMethod: DoubaoProtocol.NO_SERIALIZATION));
            taskRequest.AddRange(BitConverter.GetBytes(200).ReverseIfLittleEndian());
            taskRequest.AddRange(BitConverter.GetBytes(_sessionId.Length).ReverseIfLittleEndian());
            taskRequest.AddRange(Encoding.UTF8.GetBytes(_sessionId));

            var payloadBytes = CompressGzip(audio);
            taskRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            taskRequest.AddRange(payloadBytes);

            await SendAsync(taskRequest.ToArray(), cancellationToken);
        }

        /// <summary>
        /// 接收服务器响应
        /// </summary>
        public async Task<Dictionary<string, object>> ReceiveServerResponseAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await ReceiveAsync(cancellationToken);
                var data = DoubaoProtocol.ParseResponse(response);
                return data;
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to receive message: {e.Message}", e);
            }
        }

        /// <summary>
        /// 结束会话
        /// </summary>
        public async Task FinishSessionAsync(CancellationToken cancellationToken = default)
        {
            var finishSessionRequest = new List<byte>();
            finishSessionRequest.AddRange(DoubaoProtocol.GenerateHeader());
            finishSessionRequest.AddRange(BitConverter.GetBytes(102).ReverseIfLittleEndian());

            var payloadBytes = Encoding.UTF8.GetBytes("{}");
            payloadBytes = CompressGzip(payloadBytes);

            finishSessionRequest.AddRange(BitConverter.GetBytes(_sessionId.Length).ReverseIfLittleEndian());
            finishSessionRequest.AddRange(Encoding.UTF8.GetBytes(_sessionId));
            finishSessionRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            finishSessionRequest.AddRange(payloadBytes);

            await SendAsync(finishSessionRequest.ToArray(), cancellationToken);
        }

        /// <summary>
        /// 结束连接
        /// </summary>
        public async Task FinishConnectionAsync(CancellationToken cancellationToken = default)
        {
            var finishConnectionRequest = new List<byte>();
            finishConnectionRequest.AddRange(DoubaoProtocol.GenerateHeader());
            finishConnectionRequest.AddRange(BitConverter.GetBytes(2).ReverseIfLittleEndian());

            var payloadBytes = Encoding.UTF8.GetBytes("{}");
            payloadBytes = CompressGzip(payloadBytes);

            finishConnectionRequest.AddRange(BitConverter.GetBytes(payloadBytes.Length).ReverseIfLittleEndian());
            finishConnectionRequest.AddRange(payloadBytes);

            await SendAsync(finishConnectionRequest.ToArray(), cancellationToken);
            var response = await ReceiveAsync(cancellationToken);
            var parsedResponse = DoubaoProtocol.ParseResponse(response);
            Console.WriteLine($"FinishConnection response: {JsonSerializer.Serialize(parsedResponse)}");
        }

        /// <summary>
        /// 关闭WebSocket连接
        /// </summary>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                Console.WriteLine("Closing WebSocket connection...");
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", cancellationToken);
            }

            _webSocket?.Dispose();
        }

        /// <summary>
        /// 发送数据到WebSocket
        /// </summary>
        private async Task SendAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true,
                cancellationToken);
        }

        /// <summary>
        /// 从WebSocket接收数据
        /// </summary>
        private async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            var buffer = new byte[8192];
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("WebSocket connection closed by server");
            }

            var data = new byte[result.Count];
            Array.Copy(buffer, data, result.Count);
            return data;
        }

        /// <summary>
        /// GZIP压缩
        /// </summary>
        private static byte[] CompressGzip(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzipStream = new GZipStream(output, CompressionMode.Compress))
            {
                gzipStream.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _webSocket?.Dispose();
        }
    }

    /// <summary>
    /// BitConverter扩展方法，用于处理字节序
    /// </summary>
    internal static class BitConverterExtensions
    {
        public static byte[] ReverseIfLittleEndian(this byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }
    }
}