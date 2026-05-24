using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services.Messaging;

public static class MessageTemplateHelper
{
    public const string FirstName = "{FirstName}";
    public const string LastName = "{LastName}";
    public const string FullName = "{FullName}";
    public const string Username = "{Username}";
    public const string TelegramUserId = "{TelegramUserId}";
    public const string Phone = "{Phone}";
    public const string Plan = "{Plan}";
    public const string PlanExpiresDate = "{PlanExpiresDate}";
    public const string PlanExpiresTime = "{PlanExpiresTime}";
    public const string ExtendDays = "{ExtendDays}";

    public static string PlaceholderHelp =>
        string.Join("، ", Placeholders.Select(p => p.Token));

    public static IReadOnlyList<(string Token, string Label)> Placeholders { get; } =
    [
        (FirstName, "نام"),
        (LastName, "نام خانوادگی"),
        (FullName, "نام کامل"),
        (Username, "نام کاربری"),
        (TelegramUserId, "شناسه"),
        (Phone, "شماره"),
        (Plan, "بسته"),
        (PlanExpiresDate, "تاریخ انقضا"),
        (PlanExpiresTime, "ساعت انقضا"),
        (ExtendDays, "روز تمدید")
    ];

    public static string Render(string template, BotUser user, IReadOnlyDictionary<string, string>? extra = null)
    {
        var fullName = string.Join(" ",
            new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [FirstName] = user.FirstName ?? "",
            [LastName] = user.LastName ?? "",
            [FullName] = string.IsNullOrWhiteSpace(fullName) ? "کاربر" : fullName,
            [Username] = user.Username is { Length: > 0 } u ? $"@{u.TrimStart('@')}" : "",
            [TelegramUserId] = user.TelegramUserId.ToString(),
            [Phone] = user.PhoneNumber ?? "",
            [Plan] = MediaPlatformMapper.ToDisplayName(user.Plan),
            [PlanExpiresDate] = user.PlanExpiresAt is null
                ? "—"
                : PersianDateTimeHelper.ToShamsiDateString(user.PlanExpiresAt.Value),
            [PlanExpiresTime] = user.PlanExpiresAt is null
                ? "—"
                : PersianDateTimeHelper.ToTimeString(user.PlanExpiresAt.Value)
        };

        if (extra is not null)
        {
            foreach (var (key, value) in extra)
                map[key] = value;
        }

        var result = template;
        foreach (var (key, value) in map)
            result = result.Replace(key, value, StringComparison.Ordinal);

        return result;
    }
}
