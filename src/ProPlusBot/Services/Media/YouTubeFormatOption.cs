using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Services.Media;

public sealed record YouTubeFormatOption(
    string FormatId,
    string Label,
    string Extension,
    long? SizeBytes,
    int? Height,
    bool IsAudioOnly)
{
    public string SizeDisplay => SizeBytes is > 0
        ? ByteUnits.FormatMegabytes(SizeBytes.Value)
        : "نامشخص";

    public bool ExceedsLimit(long maxFileBytes) =>
        maxFileBytes > 0 && SizeBytes is > 0 && SizeBytes.Value > maxFileBytes;
}
