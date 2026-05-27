namespace ProPlusBot.Services.Media;

public static class UploadFallbackPresets
{
    public const int DefaultExpiryHours = 24;
    public const long DefaultMinBytes = 49L * 1024 * 1024;
    public const int MinMegabytes = 1;
    public const int MaxMegabytes = 2048;

    public static readonly int[] AllowedExpiryHours = [24, 36, 48, 72, 96, 120, 168];

    public static int NormalizeExpiryHours(int hours) =>
        AllowedExpiryHours.Contains(hours)
            ? hours
            : DefaultExpiryHours;

    public static long NormalizeMinBytes(long bytes) =>
        bytes < MegabytesToBytes(MinMegabytes)
            ? DefaultMinBytes
            : bytes > MegabytesToBytes(MaxMegabytes)
                ? MegabytesToBytes(MaxMegabytes)
                : bytes;

    public static long MegabytesToBytes(int megabytes) => (long)megabytes * 1024 * 1024;

    public static int BytesToMegabytes(long bytes) =>
        (int)Math.Clamp(bytes / (1024.0 * 1024.0), MinMegabytes, MaxMegabytes);
}
