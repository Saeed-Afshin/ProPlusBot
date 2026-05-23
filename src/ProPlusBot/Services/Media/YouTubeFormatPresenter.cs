using System.Globalization;
using System.Text;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

internal static class YouTubeFormatPresenter
{
    public static string BuildMessage(YouTubeFormatSession session)
    {
        var pageFormats = session.GetPageFormats();
        var sb = new StringBuilder();

        sb.AppendLine("کیفیت‌های موجود:");
        if (session.MaxFileBytesForPlan > 0 && session.MaxFileBytesForPlan < long.MaxValue / 2)
            sb.AppendLine($"حداکثر حجم مجاز بسته شما: {ByteUnits.FormatMegabytes(session.MaxFileBytesForPlan)}");
        sb.AppendLine();

        for (var i = 0; i < pageFormats.Count; i++)
        {
            var globalIndex = session.ToGlobalIndex(i);
            var f = pageFormats[i];
            var over = f.ExceedsLimit(session.MaxFileBytesForPlan);
            var prefix = over ? "⚠️ " : "";
            sb.AppendLine($"{globalIndex + 1}. {prefix}{f.Label} • {f.Extension} • {f.SizeDisplay}");
        }

        if (session.PageCount > 1)
            sb.AppendLine().Append($"صفحه {session.Page + 1} از {session.PageCount}");

        sb.AppendLine().Append("یک شماره را انتخاب کنید:");
        return sb.ToString().TrimEnd();
    }

    public static InlineKeyboardMarkup BuildKeyboard(YouTubeFormatSession session)
    {
        var pageFormats = session.GetPageFormats();
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < pageFormats.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();
            for (var j = i; j < Math.Min(i + 2, pageFormats.Count); j++)
            {
                var globalIndex = session.ToGlobalIndex(j);
                var f = pageFormats[j];
                var label = $"{globalIndex + 1} • {Truncate(f.Label, 12)}";
                row.Add(InlineKeyboardButton.WithCallbackData(
                    label,
                    $"{MediaConstants.CallbackYouTubeFormatPrefix}{globalIndex}"));
            }

            rows.Add(row.ToArray());
        }

        if (session.PageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();
            if (session.Page > 0)
            {
                nav.Add(InlineKeyboardButton.WithCallbackData(
                    "◀ قبلی",
                    $"{MediaConstants.CallbackYouTubeFormatPrefix}{MediaConstants.CallbackFormatPage}{session.Page - 1}"));
            }

            if (session.Page < session.PageCount - 1)
            {
                nav.Add(InlineKeyboardButton.WithCallbackData(
                    "بعدی ▶",
                    $"{MediaConstants.CallbackYouTubeFormatPrefix}{MediaConstants.CallbackFormatPage}{session.Page + 1}"));
            }

            if (nav.Count > 0)
                rows.Add(nav.ToArray());
        }

        return new InlineKeyboardMarkup(rows);
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars];
}
