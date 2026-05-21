using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services;

public class ErrorLogService(AppDbContext db)
{
    private const int MaxTitleLength = 256;
    private const int MaxSourceLength = 128;

    public async Task LogAsync(
        long? telegramUserId,
        string title,
        string detail,
        string? source = null,
        CancellationToken ct = default)
    {
        var phone = telegramUserId is null
            ? null
            : await db.BotUsers.AsNoTracking()
                .Where(u => u.TelegramUserId == telegramUserId.Value)
                .Select(u => u.PhoneNumber)
                .FirstOrDefaultAsync(ct);

        db.ErrorLogs.Add(new ErrorLog
        {
            Id = Guid.NewGuid(),
            TelegramUserId = telegramUserId,
            PhoneNumber = phone,
            Title = Truncate(title, MaxTitleLength),
            Detail = detail,
            Source = source is null ? null : Truncate(source, MaxSourceLength),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    public Task LogExceptionAsync(
        long? telegramUserId,
        string title,
        Exception ex,
        string? source = null,
        CancellationToken ct = default) =>
        LogAsync(telegramUserId, title, ex.ToString(), source, ct);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
