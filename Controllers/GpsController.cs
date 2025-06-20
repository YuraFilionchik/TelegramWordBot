using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramWordBot.Services;

namespace TelegramWordBot.Controllers;

[ApiController]
[Route("gps")]
public class GpsController : ControllerBase
{
    private readonly ITelegramBotClient _bot;

    public GpsController(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        var adminIdString = Environment.GetEnvironmentVariable("ADMIN_ID");
        if (string.IsNullOrEmpty(adminIdString) || !long.TryParse(adminIdString, out var adminId))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "ADMIN_ID is not configured");
        }

        var sb = new System.Text.StringBuilder();

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            foreach (var kvp in form)
            {
                var value = kvp.Value.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sb.AppendLine($"{kvp.Key}: {value}");
                }
            }
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                sb.AppendLine(body);
            }
        }

        var text = sb.ToString().Trim();
        if (!string.IsNullOrEmpty(text))
        {
            await _bot.SendTextMessageAsync(
                chatId: adminId,
                text: TelegramMessageHelper.EscapeHtml(text),
                parseMode: ParseMode.Html);
        }

        return Ok();
    }
}
