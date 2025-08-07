using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace EasyVoice.RealtimeDialog
{
    /// <summary>
    /// 豆包实时语音协议实现，与Python版本protocol.py功能完全对应
    /// </summary>
    public static class DoubaoProtocol
    {
        // 协议版本和头部大小
        public const byte PROTOCOL_VERSION = 0b0001;
        public const byte DEFAULT_HEADER_SIZE = 0b0001;

        // 位字段大小定义
        public const int PROTOCOL_VERSION_BITS = 4;
        public const int HEADER_BITS = 4;
        public const int MESSAGE_TYPE_BITS = 4;
        public const int MESSAGE_TYPE_SPECIFIC_FLAGS_BITS = 4;
        public const int MESSAGE_SERIALIZATION_BITS = 4;
        public const int MESSAGE_COMPRESSION_BITS = 4;
        public const int RESERVED_BITS = 8;

        // 消息类型定义
        public const byte CLIENT_FULL_REQUEST = 0b0001;
        public const byte CLIENT_AUDIO_ONLY_REQUEST = 0b0010;

        public const byte SERVER_FULL_RESPONSE = 0b1001;
        public const byte SERVER_ACK = 0b1011;
        public const byte SERVER_ERROR_RESPONSE = 0b1111;

        // 消息类型特定标志
        public const byte NO_SEQUENCE = 0b0000; // no check sequence
        public const byte POS_SEQUENCE = 0b0001;
        public const byte NEG_SEQUENCE = 0b0010;
        public const byte NEG_SEQUENCE_1 = 0b0011;

        public const byte MSG_WITH_EVENT = 0b0100;

        // 消息序列化方法
        public const byte NO_SERIALIZATION = 0b0000;
        public const byte JSON = 0b0001;
        public const byte THRIFT = 0b0011;
        public const byte CUSTOM_TYPE = 0b1111;

        // 消息压缩类型
        public const byte NO_COMPRESSION = 0b0000;
        public const byte GZIP = 0b0001;
        public const byte CUSTOM_COMPRESSION = 0b1111;

        private const byte NO_SERD = 0x00;

        /// <summary>
        /// 生成协议消息头
        /// </summary>
        /// <param name="version">协议版本</param>
        /// <param name="messageType">消息类型</param>
        /// <param name="messageTypeSpecificFlags">消息类型特定标志</param>
        /// <param name="serialMethod">序列化方法</param>
        /// <param name="compressionType">压缩类型</param>
        /// <param name="reservedData">保留数据</param>
        /// <param name="extensionHeader">扩展头</param>
        /// <returns>生成的消息头字节数组</returns>
        public static byte[] GenerateHeader(
            byte version = PROTOCOL_VERSION,
            byte messageType = CLIENT_FULL_REQUEST,
            byte messageTypeSpecificFlags = MSG_WITH_EVENT,
            byte serialMethod = JSON,
            byte compressionType = GZIP,
            byte reservedData = 0x00,
            byte[] extensionHeader = null)
        {
            extensionHeader ??= Array.Empty<byte>();

            var header = new List<byte>();
            var headerSize = (byte)(extensionHeader.Length / 4 + 1);

            // protocol_version(4 bits) + header_size(4 bits)
            header.Add((byte)((version << 4) | headerSize));

            // message_type(4 bits) + message_type_specific_flags(4 bits)
            header.Add((byte)((messageType << 4) | messageTypeSpecificFlags));

            // serialization_method(4 bits) + message_compression(4 bits)
            header.Add((byte)((serialMethod << 4) | compressionType));

            // reserved (8 bits)
            header.Add(reservedData);

            // extension header
            header.AddRange(extensionHeader);

            return header.ToArray();
        }

        /// <summary>
        /// 解析服务器响应
        /// </summary>
        /// <param name="response">响应字节数组</param>
        /// <returns>解析结果字典</returns>
        public static Dictionary<string, object> ParseResponse1(byte[] response)
        {
            if (response == null || response.Length < 4)
            {
                return new Dictionary<string, object>();
            }

            // 解析协议头
            var protocolVersion = (byte)(response[0] >> 4);
            var headerSize = (byte)(response[0] & 0x0f);
            var messageType = (byte)(response[1] >> 4);
            var messageTypeSpecificFlags = (byte)(response[1] & 0x0f);
            var serializationMethod = (byte)(response[2] >> 4);
            var messageCompression = (byte)(response[2] & 0x0f);
            var reserved = response[3];

            var headerExtensions = new byte[0];
            if (headerSize > 1)
            {
                var extensionLength = (headerSize - 1) * 4;
                headerExtensions = new byte[extensionLength];
                Array.Copy(response, 4, headerExtensions, 0, extensionLength);
            }

            var payload = new byte[response.Length - headerSize * 4];
            Array.Copy(response, headerSize * 4, payload, 0, payload.Length);

            var result = new Dictionary<string, object>();
            byte[] payloadMsg = null;
            var payloadSize = 0;
            var start = 0;

            if (messageType == SERVER_FULL_RESPONSE || messageType == SERVER_ACK)
            {
                result["message_type"] = "SERVER_FULL_RESPONSE";
                if (messageType == SERVER_ACK)
                {
                    result["message_type"] = "SERVER_ACK";
                }

                // 检查序列号
                if ((messageTypeSpecificFlags & NEG_SEQUENCE) > 0)
                {
                    var seq = BitConverter.ToUInt32(ReverseIfLittleEndian(payload, start, 4), 0);
                    result["seq"] = seq;
                    start += 4;
                }

                // 检查事件
                if ((messageTypeSpecificFlags & MSG_WITH_EVENT) > 0)
                {
                    var eventValue = BitConverter.ToUInt32(ReverseIfLittleEndian(payload, start, 4), 0);
                    result["event"] = eventValue;
                    start += 4;
                }

                // 读取会话ID（不重新分配payload，直接使用start偏移量）
                var sessionIdSize = BitConverter.ToInt32(ReverseIfLittleEndian(payload, start, 4), 0);
                var sessionIdBytes = new byte[sessionIdSize];
                Array.Copy(payload, start + 4, sessionIdBytes, 0, sessionIdSize);
                result["session_id"] = Encoding.UTF8.GetString(sessionIdBytes);
                start += 4 + sessionIdSize;

                // 读取数据长度和数据
                payloadSize = (int)BitConverter.ToUInt32(ReverseIfLittleEndian(payload, start, 4), 0);
                payloadMsg = new byte[payload.Length - start - 4];
                Array.Copy(payload, start + 4, payloadMsg, 0, payloadMsg.Length);
            }
            else if (messageType == SERVER_ERROR_RESPONSE)
            {
                var code = BitConverter.ToUInt32(ReverseIfLittleEndian(payload, 0, 4), 0);
                result["code"] = code;
                payloadSize = (int)BitConverter.ToUInt32(ReverseIfLittleEndian(payload, 4, 4), 0);
                payloadMsg = new byte[payload.Length - 8];
                Array.Copy(payload, 8, payloadMsg, 0, payloadMsg.Length);
            }

            if (payloadMsg == null)
            {
                return result;
            }

            // 解压缩
            if (messageCompression == GZIP)
            {
                payloadMsg = DecompressGzip(payloadMsg);
            }

            // 反序列化
            if (serializationMethod == JSON)
            {
                var jsonString = Encoding.UTF8.GetString(payloadMsg);
                try
                {
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    result["payload_msg"] = JsonElementToDictionary(jsonDoc.RootElement);
                }
                catch
                {
                    result["payload_msg"] = jsonString;
                }
            }
            else if (serializationMethod != NO_SERIALIZATION)
            {
                result["payload_msg"] = Encoding.UTF8.GetString(payloadMsg);
            }
            else
            {
                result["payload_msg"] = payloadMsg;
            }

            result["payload_size"] = payloadSize;
            return result;
        }

        /// <summary>
        /// GZIP解压缩
        /// </summary>
        /// <param name="data">压缩数据</param>
        /// <returns>解压缩后的数据</returns>
        private static byte[] DecompressGzip(byte[] data)
        {
            try
            {
                // 添加调试信息
                Console.WriteLine($"[DEBUG] GZIP解压缩开始，数据长度: {data.Length}");
                Console.WriteLine(
                    $"[DEBUG] 数据前16字节: {BitConverter.ToString(data.Take(Math.Min(16, data.Length)).ToArray())}");

                // 检查GZIP魔数
                if (data.Length < 2 || data[0] != 0x1f || data[1] != 0x8b)
                {
                    throw new InvalidDataException(
                        $"无效的GZIP魔数。期望: 1F-8B, 实际: {(data.Length >= 2 ? $"{data[0]:X2}-{data[1]:X2}" : "数据太短")}");
                }

                using var compressedStream = new MemoryStream(data);
                using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var resultStream = new MemoryStream();

                gzipStream.CopyTo(resultStream);
                var result = resultStream.ToArray();

                Console.WriteLine($"[DEBUG] GZIP解压缩成功，解压后长度: {result.Length}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GZIP解压缩失败: {ex.Message}");
                Console.WriteLine($"[ERROR] 异常类型: {ex.GetType().Name}");
                Console.WriteLine($"[ERROR] 数据长度: {data.Length}");
                if (data.Length > 0)
                {
                    Console.WriteLine(
                        $"[ERROR] 数据前32字节: {BitConverter.ToString(data.Take(Math.Min(32, data.Length)).ToArray())}");
                }

                throw;
            }
        }

        /// <summary>
        /// 如果是小端序则反转字节序（转换为大端序）
        /// </summary>
        /// <param name="data">源数据</param>
        /// <param name="start">起始位置</param>
        /// <param name="length">长度</param>
        /// <returns>转换后的字节数组</returns>
        private static byte[] ReverseIfLittleEndian(byte[] data, int start, int length)
        {
            var result = new byte[length];
            Array.Copy(data, start, result, 0, length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(result);
            }

            return result;
        }

        /// <summary>
        /// 将JsonElement转换为字典或基础类型
        /// </summary>
        /// <param name="element">JSON元素</param>
        /// <returns>转换后的对象</returns>
        private static object JsonElementToDictionary(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = JsonElementToDictionary(property.Value);
                    }

                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(JsonElementToDictionary(item));
                    }

                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intValue))
                        return intValue;
                    if (element.TryGetInt64(out var longValue))
                        return longValue;
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }


        private static int ToInt32BigEndian(byte[] bytes, int start)
        {
            byte[] temp = new byte[4];
            Array.Copy(bytes, start, temp, 0, 4);
            Array.Reverse(temp); // 转换为小端序
            return BitConverter.ToInt32(temp, 0);
        }

        // 辅助方法：大端序读取无符号 uint32
        private static uint ToUInt32BigEndian(byte[] bytes, int start)
        {
            byte[] temp = new byte[4];
            Array.Copy(bytes, start, temp, 0, 4);
            Array.Reverse(temp);
            return BitConverter.ToUInt32(temp, 0);
        }

        

        public static Dictionary<string, object> ParseResponse(byte[] res)
        {
            if (res == null || res.Length == 0)
            {
                return new Dictionary<string, object>();
            }

            // 头部解析（同前）
            int protocolVersion = res[0] >> 4;
            int headerSize = res[0] & 0x0f;
            int messageType = res[1] >> 4;
            int messageTypeSpecificFlags = res[1] & 0x0f;
            int serializationMethod = res[2] >> 4;
            int messageCompression = res[2] & 0x0f;
            int reserved = res[3];
            int headerExtensionLength = headerSize * 4 - 4;
            if (headerExtensionLength < 0) headerExtensionLength = 0;
            if (headerExtensionLength > res.Length - 4) headerExtensionLength = res.Length - 4;
            byte[] headerExtensions = new byte[headerExtensionLength];
            if (headerExtensionLength > 0)
            {
                Array.Copy(res, 4, headerExtensions, 0, headerExtensionLength);
            }
            int payloadLen = res.Length - headerSize * 4;
            if (payloadLen < 0) payloadLen = 0;
            byte[] payload = new byte[payloadLen];
            if (payloadLen > 0)
            {
                Array.Copy(res, headerSize * 4, payload, 0, payloadLen);
            }

            var result = new Dictionary<string, object>();
            object payloadMsg = null;
            uint payloadSize = 0;
            int start = 0;

            if (messageType == SERVER_FULL_RESPONSE || messageType == SERVER_ACK)
            {
                result["message_type"] = (messageType == SERVER_ACK) ? "SERVER_ACK" : "SERVER_FULL_RESPONSE";

                if ((messageTypeSpecificFlags & NEG_SEQUENCE) > 0 && payload.Length >= 4)
                {
                    result["seq"] = ToUInt32BigEndian(payload, 0);
                    start += 4;
                }

                if ((messageTypeSpecificFlags & MSG_WITH_EVENT) > 0 && payload.Length >= start + 4)
                {
                    result["event"] = ToUInt32BigEndian(payload, start);
                    start += 4;
                }

                byte[] remainingPayload = new byte[payload.Length - start];
                Array.Copy(payload, start, remainingPayload, 0, remainingPayload.Length);
                payload = remainingPayload;

                int sessionIdSize = 0;
                if (payload.Length >= 4)
                {
                    sessionIdSize = ToInt32BigEndian(payload, 0);
                    if (sessionIdSize < 0 || sessionIdSize > payload.Length - 4)
                    {
                        sessionIdSize = 0;
                    }
                }

                byte[] sessionIdBytes = new byte[sessionIdSize];
                Array.Copy(payload, 4, sessionIdBytes, 0, sessionIdSize);
                result["session_id"] = Encoding.UTF8.GetString(sessionIdBytes);

                remainingPayload = new byte[payload.Length - 4 - sessionIdSize];
                Array.Copy(payload, 4 + sessionIdSize, remainingPayload, 0, remainingPayload.Length);
                payload = remainingPayload;

                payloadSize = 0;
                if (payload.Length >= 4)
                {
                    payloadSize = ToUInt32BigEndian(payload, 0);
                    if (payloadSize > (uint)(payload.Length - 4))
                    {
                        payloadSize = (uint)(payload.Length - 4);
                    }
                }

                byte[] payloadMsgBytes = new byte[payloadSize];
                Array.Copy(payload, 4, payloadMsgBytes, 0, (int)payloadSize);
                payloadMsg = payloadMsgBytes;
            }
            else if (messageType == SERVER_ERROR_RESPONSE)
            {
                uint code = 0;
                if (payload.Length >= 4)
                {
                    code = ToUInt32BigEndian(payload, 0);
                }
                result["code"] = code;

                payloadSize = 0;
                if (payload.Length >= 8)
                {
                    payloadSize = ToUInt32BigEndian(payload, 4);
                    if (payloadSize > (uint)(payload.Length - 8))
                    {
                        payloadSize = (uint)(payload.Length - 8);
                    }
                }

                byte[] payloadMsgBytes = new byte[payloadSize];
                Array.Copy(payload, 8, payloadMsgBytes, 0, (int)payloadSize);
                payloadMsg = payloadMsgBytes;
            }

            if (payloadMsg == null)
            {
                return result;
            }

            byte[] payloadBytes = (byte[])payloadMsg;

            if (messageCompression == GZIP)
            {
                // 检查 GZIP 魔术字节
                if (payloadBytes.Length < 2 || payloadBytes[0] != 0x1F || payloadBytes[1] != 0x8B)
                {
                    // 不是标准 GZIP，跳过解压或处理错误（根据需要）
                    Console.WriteLine("Warning: Payload is not a valid GZIP stream.");
                }
                else
                {
                    try
                    {
                        using (var memoryStream = new MemoryStream(payloadBytes))
                        using (var decompressionStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                        using (var resultStream = new MemoryStream())
                        {
                            decompressionStream.CopyTo(resultStream);
                            payloadBytes = resultStream.ToArray();
                        }
                    }
                    catch (InvalidDataException ex)
                    {
                        Console.WriteLine($"GZIP decompression failed: {ex.Message}");
                        // 可选择不解压或抛出
                    }
                }
            }

            if (serializationMethod == JSON)
            {
                string jsonString = Encoding.UTF8.GetString(payloadBytes);
                try
                {
                    payloadMsg = JsonConvert.DeserializeObject(jsonString);
                }
                catch (Exception)
                {
                    // 若JSON解析失败，直接返回原始字符串，保持与Python实现一致
                    payloadMsg = jsonString;
                }
            }
            else if (serializationMethod != NO_SERIALIZATION)
            {
                payloadMsg = Encoding.UTF8.GetString(payloadBytes);
            }

            result["payload_msg"] = payloadMsg;
            result["payload_size"] = payloadSize;
            return result;
        }

        public static Dictionary<string, object> ParseResponse2(byte[] res)
        {
            var result = new Dictionary<string, object>();

            if (res == null || res.Length == 0)
            {
                return result;
            }

            byte protocolVersion = (byte)(res[0] >> 4);
            byte headerSize = (byte)(res[0] & 0x0F);
            byte messageType = (byte)(res[1] >> 4);
            byte messageTypeSpecificFlags = (byte)(res[1] & 0x0F);
            byte serializationMethod = (byte)(res[2] >> 4);
            byte messageCompression = (byte)(res[2] & 0x0F);
            byte reserved = res[3];

            // Extract header extensions, handle case where headerSize is 0
            byte[] headerExtensions = Array.Empty<byte>();
            if (headerSize * 4 > 4)
            {
                headerExtensions = new byte[headerSize * 4 - 4];
                Array.Copy(res, 4, headerExtensions, 0, headerSize * 4 - 4);
            }

            // Extract payload
            byte[] payload = new byte[res.Length - headerSize * 4];
            Array.Copy(res, headerSize * 4, payload, 0, res.Length - headerSize * 4);

            object payloadMsg = null;
            uint payloadSize = 0;
            int start = 0;

            if (messageType == SERVER_FULL_RESPONSE || messageType == SERVER_ACK)
            {
                result["message_type"] = messageType == SERVER_FULL_RESPONSE ? "SERVER_FULL_RESPONSE" : "SERVER_ACK";

                if ((messageTypeSpecificFlags & NEG_SEQUENCE) > 0)
                {
                    result["seq"] = BitConverter.ToUInt32(payload.Take(4).Reverse().ToArray(), 0); // Big-endian
                    start += 4;
                }

                if ((messageTypeSpecificFlags & MSG_WITH_EVENT) > 0)
                {
                    result["event"] = BitConverter.ToUInt32(payload.Take(4).Reverse().ToArray(), 0); // Big-endian
                    start += 4;
                }

                payload = payload.Skip(start).ToArray();
                int sessionIdSize = BitConverter.ToInt32(payload.Take(4).Reverse().ToArray(), 0); // Big-endian, signed
                byte[] sessionId = payload.Skip(4).Take(sessionIdSize).ToArray();
                result["session_id"] = Encoding.UTF8.GetString(sessionId);

                payload = payload.Skip(4 + sessionIdSize).ToArray();
                payloadSize = BitConverter.ToUInt32(payload.Take(4).Reverse().ToArray(), 0); // Big-endian
                payloadMsg = payload.Skip(4).ToArray();
            }
            else if (messageType == SERVER_ERROR_RESPONSE)
            {
                uint code = BitConverter.ToUInt32(payload.Take(4).Reverse().ToArray(), 0); // Big-endian
                result["code"] = code;
                payloadSize = BitConverter.ToUInt32(payload.Skip(4).Take(4).Reverse().ToArray(), 0); // Big-endian
                payloadMsg = payload.Skip(8).ToArray();
            }

            if (payloadMsg == null)
            {
                return result;
            }

            if (messageCompression == GZIP)
            {
                using (var compressedStream = new MemoryStream((byte[])payloadMsg))
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var decompressedStream = new MemoryStream())
                {
                    gzipStream.CopyTo(decompressedStream);
                    payloadMsg = decompressedStream.ToArray();
                }
            }


            // 反序列化
            if (serializationMethod == JSON)
            {
                var jsonString = Encoding.UTF8.GetString((byte[])payloadMsg);
                try
                {
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    result["payload_msg"] = JsonElementToDictionary(jsonDoc.RootElement);
                }
                catch
                {
                    result["payload_msg"] = jsonString;
                }
            }
            else if (serializationMethod != NO_SERIALIZATION)
            {
                result["payload_msg"] = Encoding.UTF8.GetString((byte[])payloadMsg);
            }
            else
            {
                result["payload_msg"] = payloadMsg;
            }

            //result["payload_msg"] = payloadMsg;
            result["payload_size"] = payloadSize;

            return result;
        }
    }
}