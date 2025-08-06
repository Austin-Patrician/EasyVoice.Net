using EasyVoice.Api.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EasyVoice.Api.Controllers
{
    [ApiController]
    [Route("api/realtime")]
    public class RealTimeController : ControllerBase
    {
        private readonly IHubContext<RealtimeDialogHub> _hubContext;
        private readonly ILogger<RealTimeController> _logger;

        public RealTimeController(
            IHubContext<RealtimeDialogHub> hubContext,
            ILogger<RealTimeController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// 获取WebSocket连接信息
        /// </summary>
        [HttpGet("websocket")]
        public IActionResult GetWebSocketInfo()
        {
            try
            {
                var scheme = Request.Scheme == "https" ? "wss" : "ws";
                var host = Request.Host;
                var websocketUrl = $"{scheme}://{host}/realtime-dialog";

                return Ok(new
                {
                    websocketUrl = websocketUrl,
                    message = "WebSocket连接地址"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取WebSocket连接信息失败");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }
}