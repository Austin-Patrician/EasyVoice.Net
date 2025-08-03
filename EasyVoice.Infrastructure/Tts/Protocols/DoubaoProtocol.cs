using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace EasyVoice.Infrastructure.Tts.Protocols;

/// <summary>
/// Defines the event type which determines the event of the message.
/// </summary>
public enum EventType : int
{
    // Default event, applicable for scenarios not using events or not requiring event transmission,
    // or for scenarios using events, non-zero values can be used to validate event legitimacy
    None = 0,

    // 1 ~ 49 for upstream Connection events
    StartConnection = 1,
    StartTask = 1, // Alias of "StartConnection"
    FinishConnection = 2,
    FinishTask = 2, // Alias of "FinishConnection"

    // 50 ~ 99 for downstream Connection events
    // Connection established successfully
    ConnectionStarted = 50,
    TaskStarted = 50, // Alias of "ConnectionStarted"
    // Connection failed (possibly due to authentication failure)
    ConnectionFailed = 51,
    TaskFailed = 51, // Alias of "ConnectionFailed"
    // Connection ended
    ConnectionFinished = 52,
    TaskFinished = 52, // Alias of "ConnectionFinished"

    // 100 ~ 149 for upstream Session events
    StartSession = 100,
    CancelSession = 101,
    FinishSession = 102,

    // 150 ~ 199 for downstream Session events
    SessionStarted = 150,
    SessionCanceled = 151,
    SessionFinished = 152,
    SessionFailed = 153,

    // Usage events
    UsageResponse = 154,
    ChargeData = 154, // Alias of "UsageResponse"

    // 200 ~ 249 for upstream general events
    TaskRequest = 200,
    UpdateConfig = 201,

    // 250 ~ 299 for downstream general events
    AudioMuted = 250,

    // 300 ~ 349 for upstream TTS events
    SayHello = 300,

    // 350 ~ 399 for downstream TTS events
    TTSSentenceStart = 350,
    TTSSentenceEnd = 351,
    TTSResponse = 352,
    TTSEnded = 359,
    PodcastRoundStart = 360,
    PodcastRoundResponse = 361,
    PodcastRoundEnd = 362,

    // 450 ~ 499 for downstream ASR events
    ASRInfo = 450,
    ASRResponse = 451,
    ASREnded = 459,

    // 500 ~ 549 for upstream dialogue events
    // (Ground-Truth-Alignment) text for speech synthesis
    ChatTTSText = 500,

    // 550 ~ 599 for downstream dialogue events
    ChatResponse = 550,
    ChatEnded = 559,

    // 650 ~ 699 for downstream dialogue events
    // Events for source (original) language subtitle
    SourceSubtitleStart = 650,
    SourceSubtitleResponse = 651,
    SourceSubtitleEnd = 652,

    // Events for target (translation) language subtitle
    TranslationSubtitleStart = 653,
    TranslationSubtitleResponse = 654,
    TranslationSubtitleEnd = 655
}

/// <summary>
/// Message type flags which determines how the message will be serialized with the protocol
/// </summary>
[Flags]
public enum MsgTypeFlagBits : byte
{
    NoSeq = 0,             // Non-terminal packet with no sequence
    PositiveSeq = 0b1,     // Non-terminal packet with sequence > 0
    LastNoSeq = 0b10,      // last packet with no sequence
    NegativeSeq = 0b11,    // last packet with sequence < 0
    WithEvent = 0b100      // Payload contains event number (int32)
}

/// <summary>
/// Version bits defines the 4-bit version type
/// </summary>
public enum VersionBits : byte
{
    Version1 = 1,
    Version2 = 2,
    Version3 = 3,
    Version4 = 4
}

/// <summary>
/// Header size bits defines the 4-bit header-size type
/// </summary>
public enum HeaderSizeBits : byte
{
    HeaderSize4 = 1,
    HeaderSize8 = 2,
    HeaderSize12 = 3,
    HeaderSize16 = 4
}

/// <summary>
/// Serialization bits defines the 4-bit serialization method type
/// </summary>
public enum SerializationBits : byte
{
    Raw = 0,
    JSON = 0b1,
    Thrift = 0b11,
    Custom = 0b1111
}

/// <summary>
/// Compression bits defines the 4-bit compression method type
/// </summary>
public enum CompressionBits : byte
{
    None = 0,
    Gzip = 0b1,
    Custom = 0b1111
}

/// <summary>
/// Message type which determines how the message will be serialized with the protocol
/// </summary>
public enum MsgType : byte
{
    Invalid = 0,
    FullClientRequest = 0b1,
    AudioOnlyClient = 0b10,
    FullServerResponse = 0b1001,
    AudioOnlyServer = 0b1011,
    FrontEndResultServer = 0b1100,
    Error = 0b1111,
    ServerACK = AudioOnlyServer
}

/// <summary>
/// Message structure for Doubao protocol communication
/// </summary>
public class DoubaoMessage
{
    public VersionBits Version { get; set; }
    public HeaderSizeBits HeaderSize { get; set; }
    public MsgType MsgType { get; set; }
    public MsgTypeFlagBits MsgTypeFlag { get; set; }
    public SerializationBits Serialization { get; set; }
    public CompressionBits Compression { get; set; }

    public EventType EventType { get; set; }
    public string? SessionId { get; set; }
    public string? ConnectId { get; set; }
    public int Sequence { get; set; }
    public uint ErrorCode { get; set; }

    public byte[] Payload { get; set; }

    /// <summary>
    /// Creates a new message with default values
    /// </summary>
    public DoubaoMessage()
    {
        Version = VersionBits.Version1;
        HeaderSize = HeaderSizeBits.HeaderSize4;
        Serialization = SerializationBits.JSON;
        Compression = CompressionBits.None;
        Payload = Array.Empty<byte>();
    }

    /// <summary>
    /// Creates a new message with specified message type and flag
    /// </summary>
    public static DoubaoMessage Create(MsgType msgType, MsgTypeFlagBits flag)
    {
        return new DoubaoMessage
        {
            MsgType = msgType,
            MsgTypeFlag = flag
        };
    }

    /// <summary>
    /// Creates a message from byte array
    /// </summary>
    public static DoubaoMessage FromBytes(byte[] data)
    {
        if (data == null || data.Length < 4)
        {
            throw new ArgumentException("Invalid data length", nameof(data));
        }

        var message = new DoubaoMessage();
        using var stream = new MemoryStream(data);
        message.Unmarshal(stream);
        return message;
    }

    /// <summary>
    /// Converts the message to a byte array
    /// </summary>
    public byte[] Marshal()
    {
        using var stream = new MemoryStream();

        // Write header bytes
        byte header1 = (byte)((byte)Version << 4 | (byte)HeaderSize);
        byte header2 = (byte)((byte)MsgType << 4 | (byte)MsgTypeFlag);
        byte header3 = (byte)((byte)Serialization << 4 | (byte)Compression);

        stream.WriteByte(header1);
        stream.WriteByte(header2);
        stream.WriteByte(header3);

        // Write padding for header size
        int headerSize = 4 * (int)HeaderSize;
        int paddingSize = headerSize - 3;
        for (int i = 0; i < paddingSize; i++)
        {
            stream.WriteByte(0);
        }

        // Write fields in Go writers() order
        if ((MsgTypeFlag & MsgTypeFlagBits.WithEvent) != 0)
        {
            // Write event type
            var eventBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(eventBytes, (int)EventType);
            stream.Write(eventBytes, 0, 4);

            // Write session ID
            WriteSessionId(stream);
        }

        // Write sequence if needed
        switch (MsgType)
        {
            case MsgType.FullClientRequest:
            case MsgType.FullServerResponse:
            case MsgType.FrontEndResultServer:
            case MsgType.AudioOnlyClient:
            case MsgType.AudioOnlyServer:
                if (MsgTypeFlag == MsgTypeFlagBits.PositiveSeq || MsgTypeFlag == MsgTypeFlagBits.NegativeSeq)
                {
                    var seqBytes = new byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(seqBytes, Sequence);
                    stream.Write(seqBytes, 0, 4);
                }
                break;

            case MsgType.Error:
                var errorBytes = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(errorBytes, ErrorCode);
                stream.Write(errorBytes, 0, 4);
                break;
        }

        // Write payload with length prefix
        WritePayload(stream);

        return stream.ToArray();
    }

    private void WriteSessionId(MemoryStream stream)
    {
        // Skip session ID for connection events
        switch (EventType)
        {
            case EventType.StartConnection:
            case EventType.FinishConnection:
            case EventType.ConnectionStarted:
            case EventType.ConnectionFailed:
                return;
        }

        var sessionBytes = string.IsNullOrEmpty(SessionId) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(SessionId!);
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)sessionBytes.Length);
        stream.Write(lenBytes, 0, 4);
        if (sessionBytes.Length > 0)
        {
            stream.Write(sessionBytes, 0, sessionBytes.Length);
        }
    }

    private void WritePayload(MemoryStream stream)
    {
        var payloadBytes = Payload ?? Array.Empty<byte>();
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)payloadBytes.Length);
        stream.Write(lenBytes, 0, 4);
        if (payloadBytes.Length > 0)
        {
            stream.Write(payloadBytes, 0, payloadBytes.Length);
        }
    }

    /// <summary>
    /// Unmarshals a byte array into the message
    /// </summary>
    private void Unmarshal(MemoryStream stream)
    {
        // Read header bytes
        int header1 = stream.ReadByte();
        Version = (VersionBits)(header1 >> 4);
        HeaderSize = (HeaderSizeBits)(header1 & 0x0F);

        int header2 = stream.ReadByte();
        MsgType = (MsgType)(header2 >> 4);
        MsgTypeFlag = (MsgTypeFlagBits)(header2 & 0x0F);

        int header3 = stream.ReadByte();
        Serialization = (SerializationBits)(header3 >> 4);
        Compression = (CompressionBits)(header3 & 0x0F);

        // Skip padding bytes
        int headerSize = 4 * (int)HeaderSize;
        int paddingSize = headerSize - 3;
        for (int i = 0; i < paddingSize; i++)
        {
            stream.ReadByte();
        }

        // Read fields in Go readers() order

        // First, read sequence or error code based on message type
        switch (MsgType)
        {
            case MsgType.FullClientRequest:
            case MsgType.FullServerResponse:
            case MsgType.FrontEndResultServer:
            case MsgType.AudioOnlyClient:
            case MsgType.AudioOnlyServer:
                if (MsgTypeFlag == MsgTypeFlagBits.PositiveSeq || MsgTypeFlag == MsgTypeFlagBits.NegativeSeq)
                {
                    var seqBytes = new byte[4];
                    stream.Read(seqBytes, 0, 4);
                    Sequence = BinaryPrimitives.ReadInt32BigEndian(seqBytes);
                }
                break;

            case MsgType.Error:
                var errorBytes = new byte[4];
                stream.Read(errorBytes, 0, 4);
                ErrorCode = BinaryPrimitives.ReadUInt32BigEndian(errorBytes);
                break;

            default:
                throw new InvalidDataException($"Unsupported message type: {MsgType}");
        }

        // Then, if WithEvent flag is set, read event, session ID, and connect ID
        if ((MsgTypeFlag & MsgTypeFlagBits.WithEvent) != 0)
        {
            var eventBytes = new byte[4];
            stream.Read(eventBytes, 0, 4);
            EventType = (EventType)BinaryPrimitives.ReadInt32BigEndian(eventBytes);

            ReadSessionId(stream);
            ReadConnectId(stream);
        }

        // Read payload with length prefix
        ReadPayload(stream);

        // Verify no unexpected data remains
        if (stream.Position < stream.Length)
        {
            throw new InvalidDataException($"Unexpected data after message: {stream.Length - stream.Position} bytes remaining");
        }
    }

    private void ReadSessionId(MemoryStream stream)
    {
        // Skip session ID for connection events
        switch (EventType)
        {
            case EventType.StartConnection:
            case EventType.FinishConnection:
            case EventType.ConnectionStarted:
            case EventType.ConnectionFailed:
            case EventType.ConnectionFinished:
                return;
        }

        var lenBytes = new byte[4];
        stream.Read(lenBytes, 0, 4);
        uint sessionIdLength = BinaryPrimitives.ReadUInt32BigEndian(lenBytes);

        if (sessionIdLength > 0)
        {
            var sessionBytes = new byte[sessionIdLength];
            stream.Read(sessionBytes, 0, (int)sessionIdLength);
            SessionId = Encoding.UTF8.GetString(sessionBytes);
        }
    }

    private void ReadConnectId(MemoryStream stream)
    {
        // Only read connect ID for specific connection events
        switch (EventType)
        {
            case EventType.ConnectionStarted:
            case EventType.ConnectionFailed:
            case EventType.ConnectionFinished:
                break;
            default:
                return;
        }

        var lenBytes = new byte[4];
        stream.Read(lenBytes, 0, 4);
        uint connectIdLength = BinaryPrimitives.ReadUInt32BigEndian(lenBytes);

        if (connectIdLength > 0)
        {
            var connectBytes = new byte[connectIdLength];
            stream.Read(connectBytes, 0, (int)connectIdLength);
            ConnectId = Encoding.UTF8.GetString(connectBytes);
        }
    }

    private void ReadPayload(MemoryStream stream)
    {
        var lenBytes = new byte[4];
        stream.Read(lenBytes, 0, 4);
        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(lenBytes);

        if (payloadLength > 0)
        {
            Payload = new byte[payloadLength];
            stream.Read(Payload, 0, (int)payloadLength);
        }
        else
        {
            Payload = Array.Empty<byte>();
        }
    }

    public override string ToString()
    {
        switch (MsgType)
        {
            case MsgType.AudioOnlyServer:
            case MsgType.AudioOnlyClient:
                if (MsgTypeFlag == MsgTypeFlagBits.PositiveSeq || MsgTypeFlag == MsgTypeFlagBits.NegativeSeq)
                {
                    return $"MsgType: {MsgType}, EventType: {EventType}, Sequence: {Sequence}, PayloadSize: {Payload.Length}";
                }
                return $"MsgType: {MsgType}, EventType: {EventType}, PayloadSize: {Payload.Length}";

            case MsgType.Error:
                return $"MsgType: {MsgType}, EventType: {EventType}, ErrorCode: {ErrorCode}, Payload: {GetPayloadString()}";

            default:
                if (MsgTypeFlag == MsgTypeFlagBits.PositiveSeq || MsgTypeFlag == MsgTypeFlagBits.NegativeSeq)
                {
                    return $"MsgType: {MsgType}, EventType: {EventType}, Sequence: {Sequence}, Payload: {GetPayloadString()}";
                }
                return $"MsgType: {MsgType}, EventType: {EventType}, Payload: {GetPayloadString()}";
        }
    }

    private string GetPayloadString()
    {
        if (Payload == null || Payload.Length == 0)
            return "";
        try
        {
            return Encoding.UTF8.GetString(Payload);
        }
        catch
        {
            return Convert.ToHexString(Payload);
        }
    }
}