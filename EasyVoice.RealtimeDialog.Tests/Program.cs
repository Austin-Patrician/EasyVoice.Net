using Microsoft.Extensions.Logging;

namespace EasyVoice.RealtimeDialog.Tests;

class Program
{
    public static void Main(string[] args)
    {
        // 创建日志工厂
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 获取日志记录器
        var logger = loggerFactory.CreateLogger<Program>();

        // 输出测试信息
        logger.LogInformation("EasyVoice.RealtimeDialog 测试程序已启动。");
    }
}