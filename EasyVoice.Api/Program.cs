using FFMpegCore;
using Microsoft.AspNetCore.WebSockets;
using EasyVoice.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Configure FFmpeg path for macOS Homebrew installation
GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "/opt/homebrew/bin/" });

// Add services to the container.
builder.Services.AddHttpClient();

// Register application services
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.IStorageService, EasyVoice.Infrastructure.Storage.MemoryStorageService>();
builder.Services.AddSingleton(new EasyVoice.Infrastructure.Caching.AudioCacheOptions());
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.IAudioCacheService, EasyVoice.Infrastructure.Caching.AudioCacheService>();

// Register new audio concatenation service
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.IAudioConcatenationService, EasyVoice.Infrastructure.Audio.AudioConcatenationService>();

// Register TTS engines
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.Tts.ITtsEngine, EasyVoice.Infrastructure.Tts.Engines.EdgeTtsEngine>();

// Configure OpenAI TTS options
var openAiOptions = new EasyVoice.Infrastructure.Tts.Engines.OpenAiTtsOptions
{
    ApiKey = builder.Configuration.GetSection("OpenAI")["ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    BaseUrl = "https://api.token-ai.cn/v1",
    ModelName = "gpt-4o-mini-tts",
    TimeoutSeconds = 180
};
builder.Services.AddSingleton(openAiOptions);

// Register OpenAI TTS engine with dedicated HttpClient
builder.Services.AddHttpClient<EasyVoice.Infrastructure.Tts.Engines.OpenAiTtsEngine>();
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.Tts.ITtsEngine>(serviceProvider =>
{
    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(EasyVoice.Infrastructure.Tts.Engines.OpenAiTtsEngine));
    var logger = serviceProvider.GetService<ILogger<EasyVoice.Infrastructure.Tts.Engines.OpenAiTtsEngine>>();
    return new EasyVoice.Infrastructure.Tts.Engines.OpenAiTtsEngine(openAiOptions, httpClient, logger);
});

builder.Services.AddSingleton<EasyVoice.Core.Interfaces.Tts.ITtsEngine, EasyVoice.Infrastructure.Tts.Engines.KokoroTtsEngine>();

// Configure Doubao TTS options
var doubaoOptions = new EasyVoice.Infrastructure.Tts.Engines.DoubaoTtsOptions
{
    AppId = builder.Configuration.GetSection("Doubao")["AppId"] ?? Environment.GetEnvironmentVariable("DOUBAO_APP_ID"),
    AccessToken = builder.Configuration.GetSection("Doubao")["AccessToken"] ?? Environment.GetEnvironmentVariable("DOUBAO_ACCESS_TOKEN"),
    Cluster = builder.Configuration.GetSection("Doubao")["Cluster"],
    VoiceType = builder.Configuration.GetSection("Doubao")["VoiceType"] ?? "zh_female_1",
    AudioEncoding = builder.Configuration.GetSection("Doubao")["AudioEncoding"] ?? "wav",
    Endpoint = builder.Configuration.GetSection("Doubao")["Endpoint"] ?? "wss://openspeech.bytedance.com/api/v1/tts/ws_binary",
    TimeoutSeconds = int.TryParse(builder.Configuration.GetSection("Doubao")["TimeoutSeconds"], out var timeout) ? timeout : 30
};
builder.Services.AddSingleton(doubaoOptions);

// Register Doubao TTS engine
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.Tts.ITtsEngine>(serviceProvider =>
{
    var logger = serviceProvider.GetService<ILogger<EasyVoice.Infrastructure.Tts.Engines.DoubaoTtsEngine>>();
    return new EasyVoice.Infrastructure.Tts.Engines.DoubaoTtsEngine(doubaoOptions, logger);
});

builder.Services.AddSingleton<EasyVoice.Core.Interfaces.Tts.ITtsEngineFactory, EasyVoice.Infrastructure.Tts.TtsEngineFactory>();
builder.Services.AddSingleton<EasyVoice.Core.Interfaces.ITextService, EasyVoice.Core.Services.TextService>();
builder.Services.AddScoped<EasyVoice.Core.Interfaces.ITtsService, EasyVoice.Core.Services.TtsService>();
builder.Services.AddScoped<EasyVoice.Core.Interfaces.ILlmService, EasyVoice.Core.Services.LlmService>();
builder.Services.AddScoped<EasyVoice.Core.Interfaces.IAnalysisTextService, EasyVoice.Core.Services.AnalysisTextService>();

// Register Real-time Dialog services
builder.Services.AddMemoryCache();

// Register Realtime Dialog services
builder.Services.AddSingleton<EasyVoice.RealtimeDialog.Services.DoubaoProtocolHandler>();
builder.Services.AddSingleton<EasyVoice.RealtimeDialog.Services.WebSocketClientManager>();
builder.Services.AddSingleton<EasyVoice.RealtimeDialog.Services.RealtimeDialogService>();
builder.Services.AddSingleton<EasyVoice.RealtimeDialog.Services.AudioService>();

// Add SignalR support
builder.Services.AddSignalR();

// Add controllers to the container.
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add WebSocket support
builder.Services.AddWebSockets(options =>
{
    options.KeepAliveInterval = TimeSpan.FromMinutes(2);
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();

// Enable WebSocket support
app.UseWebSockets();

app.UseAuthorization();

// Map SignalR hubs
app.MapHub<RealtimeDialogHub>("/hubs/realtime-dialog");
// Map RealTime WebSocket endpoint for frontend integration
app.MapHub<RealtimeDialogHub>("/api/realtime/ws");

app.MapControllers();

app.Run();
