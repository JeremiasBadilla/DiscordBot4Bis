using DiscordBotAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBotAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BotController : ControllerBase
    {
        private readonly DiscordBotService _discordBotService;

        public BotController(DiscordBotService discordBotService)
        {
            _discordBotService = discordBotService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartBot()
        {
            try
            {
                await _discordBotService.StartAsync();
                return Ok("Bot iniciado correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error al iniciar el bot: {ex}");
                return StatusCode(500, "Error al iniciar el bot.");
            }
        }
    }
}

