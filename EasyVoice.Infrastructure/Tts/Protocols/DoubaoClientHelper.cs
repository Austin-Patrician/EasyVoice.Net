using System.Net.WebSockets;

namespace EasyVoice.Infrastructure.Tts.Protocols;

/// <summary>
/// Helper class for Doubao WebSocket client operations
/// </summary>
public class DoubaoClientHelper
{
    public static async Task SendMessage(ClientWebSocket webSocket, DoubaoMessage message, CancellationToken cancellationToken)
    {
        var data = message.Marshal();
        await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, cancellationToken);
    }

    public static async Task<DoubaoMessage> ReceiveMessage(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192]; // Increased buffer size for audio data
        var segments = new List<byte>();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException($"Server closed connection: {result.CloseStatus} - {result.CloseStatusDescription}");
            }

            segments.AddRange(buffer.Take(result.Count));

            if (result.EndOfMessage)
            {
                break;
            }
        }

        return DoubaoMessage.FromBytes(segments.ToArray());
    }

    public static async Task FullClientRequest(ClientWebSocket webSocket, byte[] payload, CancellationToken cancellationToken)
    {
        var message = DoubaoMessage.Create(MsgType.FullClientRequest, MsgTypeFlagBits.NoSeq);
        message.Payload = payload;
        await SendMessage(webSocket, message, cancellationToken);
    }
}