using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Controllers;

[ApiController]
[Route("api/bot-settings")]
[Authorize(Policy = AuthConstants.Scheme)]
public class BotSettingsController(BotSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(BotSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BotSettingsDto>> Get(CancellationToken ct)
    {
        var s = await settingsService.GetAsync(ct);
        return Ok(new BotSettingsDto(
            s.Mode,
            s.UpdateMode,
            s.IsActive,
            s.YouTubeEnabled,
            s.PinterestEnabled,
            s.SearchGridColumns,
            s.SearchGridRows,
            s.SearchGridJpegQuality,
            s.ConversationStateBackend,
            s.WebhookUrl,
            s.UpdatedAt));
    }

    [HttpPut]
    [ProducesResponseType(typeof(BotSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BotSettingsDto>> Update([FromBody] UpdateBotSettingsRequest request, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return Forbid();

        var s = await settingsService.UpdateAsync(
            request.Mode,
            request.UpdateMode,
            request.IsActive,
            request.WebhookUrl,
            request.YouTubeEnabled,
            request.PinterestEnabled,
            request.SearchGridColumns,
            request.SearchGridRows,
            request.SearchGridJpegQuality,
            request.ConversationStateBackend,
            User.GetAdminId(),
            ct);

        return Ok(new BotSettingsDto(
            s.Mode,
            s.UpdateMode,
            s.IsActive,
            s.YouTubeEnabled,
            s.PinterestEnabled,
            s.SearchGridColumns,
            s.SearchGridRows,
            s.SearchGridJpegQuality,
            s.ConversationStateBackend,
            s.WebhookUrl,
            s.UpdatedAt));
    }
}
