using Microsoft.Extensions.Logging;

namespace EasyVoice.RealtimeDialog.TestConsole;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        // 配置日志记录
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<Program>();
        
        logger.LogInformation("启动豆包实时对话测试程序...");

        try
        {
            // 创建音频管理器实例
            using var audioManager = new DoubaoAudioManager(
                Config.WsConnectConfig,
                Config.InputAudioConfig,
                Config.OutputAudioConfig);

            logger.LogInformation("音频管理器已创建，开始启动对话会话...");

            // 启动对话会话
            await audioManager.StartAsync();

            logger.LogInformation("对话会话已完成");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "程序执行过程中发生错误: {Message}", ex.Message);
            Console.WriteLine($"错误: {ex.Message}");
            Environment.Exit(1);
        }

        logger.LogInformation("程序执行完成，按任意键退出...");
        Console.ReadKey();
    }
}