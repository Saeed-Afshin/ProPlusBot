using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProPlusBot.Auth;
using ProPlusBot.Data;
using ProPlusBot.Models;

namespace ProPlusBot.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize(Policy = AuthConstants.Scheme)]
public class ChatsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatMessageDto>>> List(
        [FromQuery] long? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!User.CanAccessAdminPanel())
            return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.ChatMessages.AsNoTracking();
        if (userId.HasValue)
            query = query.Where(m => m.TelegramUserId == userId.Value);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ChatMessageDto(m.Id, m.TelegramUserId, m.Direction, m.Text, m.MessageType, m.CreatedAt))
            .ToListAsync(ct);

        return Ok(items);
    }
}
