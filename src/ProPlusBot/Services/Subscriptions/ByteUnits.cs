using ProPlusBot.Entities;

namespace ProPlusBot.Services.Subscriptions;

public static class ByteUnits
{
    public const long BytesPerMegabyte = 1024 * 1024;

    public static decimal ToMegabytes(long bytes) =>
        bytes <= 0 ? 0 : Math.Round(bytes / (decimal)BytesPerMegabyte, 2);

    public static long FromMegabytes(decimal megabytes) =>
        megabytes <= 0 ? 0 : (long)Math.Round(megabytes * BytesPerMegabyte, MidpointRounding.AwayFromZero);

    public static string FormatMegabytes(long bytes) =>
        bytes <= 0 ? "0 MB" : $"{ToMegabytes(bytes):0.##} MB";

    public static bool IsByteLimitKind(QuotaLimitKind kind) =>
        kind is QuotaLimitKind.DownloadBytes or QuotaLimitKind.MaxFileBytes;
}
