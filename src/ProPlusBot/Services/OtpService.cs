using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using Telegram.Bot;

namespace ProPlusBot.Services;

public class OtpService(
    AppDbContext db,
    BaleBotClientFactory clientFactory,
    RoleResolverService roleResolver,
    ILogger<OtpService> logger)
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    public async Task<(bool Success, string? Error)> SendLoginOtpAsync(string phoneNumber, CancellationToken ct = default)
    {
        var normalized = PhoneNormalizer.Normalize(phoneNumber);

        long telegramUserId;

        if (roleResolver.IsSuperAdminPhone(normalized))
        {
            var botUser = await db.BotUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == normalized && u.HasSharedPhone, ct);

            if (botUser is null)
                return (false, "ابتدا شماره خود را با دکمه اشتراک تماس در ربات ارسال کنید.");

            telegramUserId = botUser.TelegramUserId;
        }
        else
        {
            var admin = await db.AdminUsers
                .FirstOrDefaultAsync(a => a.PhoneNumber == normalized && a.IsActive, ct);

            if (admin is null)
                return (false, "شماره تماس مجاز نیست.");

            if (admin.Role == AdminRole.Tester)
                return (false, "فقط ادمین و سوپر ادمین می‌توانند وارد پنل شوند.");

            telegramUserId = admin.TelegramUserId;
        }

        var code = Random.Shared.Next(100000, 999999).ToString();
        db.OtpSessions.Add(new OtpSession
        {
            Id = Guid.NewGuid(),
            PhoneNumber = normalized,
            Code = code,
            ExpiresAt = DateTime.UtcNow.Add(OtpLifetime),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        try
        {
            var bot = clientFactory.CreateClient();
            await bot.SendMessage(
                telegramUserId,
                $"کد ورود به پنل مدیریت: {code}\nاین کد تا ۵ دقیقه معتبر است.",
                cancellationToken: ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OTP for {Phone}", normalized);
            return (false, "ارسال کد از طریق ربات ناموفق بود. با ربات گفتگو کنید و دوباره تلاش کنید.");
        }
    }

    public async Task<AuthenticatedAdmin?> ValidateOtpAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        var normalized = PhoneNormalizer.Normalize(phoneNumber);
        var session = await db.OtpSessions
            .Where(s => s.PhoneNumber == normalized && !s.IsUsed && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null || session.Code != code.Trim())
            return null;

        session.IsUsed = true;
        await db.SaveChangesAsync(ct);

        if (roleResolver.IsSuperAdminPhone(normalized))
        {
            var botUser = await db.BotUsers
                .FirstOrDefaultAsync(u => u.PhoneNumber == normalized && u.HasSharedPhone, ct);

            if (botUser is null)
                return null;

            return new AuthenticatedAdmin(
                null,
                botUser.TelegramUserId,
                normalized,
                "Super Admin",
                AdminRole.SuperAdmin,
                IsConfigSuperAdmin: true);
        }

        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.PhoneNumber == normalized && a.IsActive, ct);

        return admin is null
            ? null
            : new AuthenticatedAdmin(admin.Id, admin.TelegramUserId, admin.PhoneNumber, admin.DisplayName, admin.Role);
    }
}
