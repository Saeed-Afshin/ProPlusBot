using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Services;
using Telegram.Bot.Types;

namespace ProPlusBot.Controllers;

[ApiController]
[Route("api/bot")]
public class BotWebhookController(
    BotUpdateHandler updateHandler,
    BotSettingsService settingsService,
    IOptions<BotOptions> botOptions) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        [FromBody] Update update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
        CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        if (!settings.IsActive || settings.UpdateMode != BotUpdateMode.Webhook)
            return Ok();

        var expectedSecret = botOptions.Value.WebhookSecret;
        if (!string.IsNullOrEmpty(expectedSecret) && secretToken != expectedSecret)
            return Unauthorized();

        await updateHandler.HandleUpdateAsync(update, ct);
        return Ok();
    }
}
