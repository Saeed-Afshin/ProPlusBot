namespace ProPlusBot.Services.Subscriptions;

public static class PersianTextHelper
{
    private const char LeftToRightMark = '\u200E';

    /// <summary>Wraps Latin digits/units so they render cleanly in RTL Telegram messages.</summary>
    public static string IsolateLtr(string value) => $"{LeftToRightMark}{value}{LeftToRightMark}";

    public static string UsedOf(long used, long limit)
    {
        var usedText = IsolateLtr(used.ToString());
        if (limit <= 0)
            return $"{usedText} (سقف: نامحدود)";

        return $"{usedText} از {IsolateLtr(limit.ToString())}";
    }
}
