namespace EasyVoice.RealtimeDialog.Models.Protocol;

public class AudioConfig
{
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitDepth { get; set; }
    public string Format { get; set; }  
    public int ChunkSize { get; set; }
}