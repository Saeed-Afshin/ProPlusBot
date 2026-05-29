using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public static class ByteUnits
{
    public const long BytesPerMegabyte = 1024 * 1024;
    private const decimal MegabytesPerGigabyte = 1024m;

    public static decimal ToMegabytes(long bytes) =>
        bytes <= 0 ? 0 : Math.Round(bytes / (decimal)BytesPerMegabyte, 2);

    public static long FromMegabytes(decimal megabytes) =>
        megabytes <= 0 ? 0 : (long)Math.Round(megabytes * BytesPerMegabyte, MidpointRounding.AwayFromZero);

    /// <summary>Admin tables and technical display (always MB).</summary>
    public static string FormatMegabytes(long bytes) =>
        bytes <= 0 ? "0 MB" : $"{ToMegabytes(bytes):0.##} MB";

    /// <summary>Bot-facing volume: uses GB when size is above 1024 MB.</summary>
    public static string FormatVolume(long bytes)
    {
        if (bytes <= 0)
            return "0 MB";

        var mb = bytes / (decimal)BytesPerMegabyte;
        if (mb > MegabytesPerGigabyte)
        {
            var gb = Math.Round(mb / MegabytesPerGigabyte, 1, MidpointRounding.AwayFromZero);
            var text = gb % 1 == 0 ? $"{gb:0}" : $"{gb:0.#}";
            return $"{text} GB";
        }

        return $"{Math.Round(mb, mb >= 100 ? 0 : 1, MidpointRounding.AwayFromZero):0.#} MB";
    }

    public static bool IsByteLimitKind(QuotaLimitKind kind) =>
        kind is QuotaLimitKind.DownloadBytes;

    /// <summary>Formats used/limit volumes in the same unit (MB or GB) for quota summaries.</summary>
    public static string FormatVolumeUsedOf(long usedBytes, long limitBytes)
    {
        if (limitBytes <= 0 && usedBytes <= 0)
            return PersianTextHelper.IsolateLtr("0 MB");

        if (limitBytes <= 0)
            return $"{PersianTextHelper.IsolateLtr(FormatVolume(usedBytes))} (سقف: نامحدود)";

        var useGigabytes = Math.Max(usedBytes, limitBytes) > BytesPerMegabyte * (long)MegabytesPerGigabyte;
        var used = FormatVolumeInUnit(usedBytes, useGigabytes);
        var limit = FormatVolumeInUnit(limitBytes, useGigabytes);
        return $"{PersianTextHelper.IsolateLtr(used)} از {PersianTextHelper.IsolateLtr(limit)}";
    }

    private static string FormatVolumeInUnit(long bytes, bool useGigabytes)
    {
        if (bytes <= 0)
            return useGigabytes ? "0 GB" : "0 MB";

        var mb = bytes / (decimal)BytesPerMegabyte;
        if (useGigabytes)
        {
            var gb = Math.Round(mb / MegabytesPerGigabyte, 1, MidpointRounding.AwayFromZero);
            var text = gb % 1 == 0 ? $"{gb:0}" : $"{gb:0.#}";
            return $"{text} GB";
        }

        return $"{Math.Round(mb, mb >= 100 ? 0 : 1, MidpointRounding.AwayFromZero):0.#} MB";
    }
}
