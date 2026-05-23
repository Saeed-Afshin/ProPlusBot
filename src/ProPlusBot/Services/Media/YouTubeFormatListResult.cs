namespace ProPlusBot.Services.Media;

public sealed record YouTubeFormatListResult(
    bool Success,
    IReadOnlyList<YouTubeFormatOption> Formats,
    string? ErrorDetail)
{
    public static YouTubeFormatListResult Ok(IReadOnlyList<YouTubeFormatOption> formats) =>
        new(true, formats, null);

    public static YouTubeFormatListResult Failed(string error) =>
        new(false, [], error);
}
