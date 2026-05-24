using System.Text;
using ProPlusBot.Services.Subscriptions;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProPlusBot.Services.Media;

internal static class YouTubeFormatPresenter
{
    public static string BuildMessage(YouTubeFormatSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("کیفیت را انتخاب کنید:");
        if (session.MaxFileBytesForPlan > 0 && session.MaxFileBytesForPlan < long.MaxValue / 2)
            sb.AppendLine($"حداکثر حجم مجاز بسته: {ByteUnits.FormatVolume(session.MaxFileBytesForPlan)}");
        if (session.PageCount > 1)
            sb.AppendLine($"صفحه {session.Page + 1} از {session.PageCount} — کوچک‌ترین حجم اول");
        return sb.ToString().TrimEnd();
    }

    public static InlineKeyboardMarkup BuildKeyboard(YouTubeFormatSession session)
    {
        var pageFormats = session.GetPageFormats();
        var rows = new List<InlineKeyboardButton[]>();
        var columns = MediaConstants.YouTubeFormatKeyboardColumns;

        for (var i = 0; i < pageFormats.Count; i += columns)
        {
            var row = new List<InlineKeyboardButton>();
            for (var j = i; j < Math.Min(i + columns, pageFormats.Count); j++)
            {
                var globalIndex = session.ToGlobalIndex(j);
                var f = pageFormats[j];
                row.Add(InlineKeyboardButton.WithCallbackData(
                    BuildButtonLabel(f),
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
                    MediaConstants.NextPageButtonText,
                    $"{MediaConstants.CallbackYouTubeFormatPrefix}{MediaConstants.CallbackFormatPage}{session.Page + 1}"));
            }

            if (nav.Count > 0)
                rows.Add(nav.ToArray());
        }

        return new InlineKeyboardMarkup(rows);
    }

    public static string BuildButtonLabel(YouTubeFormatOption format)
    {
        var text = $"{format.Label} • {format.Extension} • {format.SizeDisplay}";
        return Truncate(text, 64);
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars];
}
