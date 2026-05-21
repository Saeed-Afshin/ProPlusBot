using System.Text.RegularExpressions;

namespace ProPlusBot.Services.Media;

public enum DetectedMediaPlatform
{
    YouTube,
    Pinterest
}

public readonly record struct DetectedMediaUrl(DetectedMediaPlatform Platform, string Url);

public static partial class MediaUrlDetector
{
    [GeneratedRegex(
        @"https?://(?:www\.)?(?:youtube\.com/watch\?[^\s]*v=[\w-]+|youtu\.be/[\w-]+|youtube\.com/shorts/[\w-]+|m\.youtube\.com/watch\?[^\s]*v=[\w-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex YouTubeRegex();

    [GeneratedRegex(
        @"https?://(?:www\.)?pin\.it/[\w]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PinItRegex();

    [GeneratedRegex(
        @"https?://(?:[\w-]+\.)?pinterest\.(?:com|it|co\.uk|de|fr|jp|ca|au|nz|ie|ch|at|pt|es|se|be|nl|dk|fi|no|pl|ru|kr|cl|mx|in|co|ph|vn|th|id|my|sg|hk|tw)/pin/[\w-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PinterestPinRegex();

    [GeneratedRegex(
        @"https?://(?:[\w-]+\.)?pinterest\.[\w.]+/[\w@./%-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PinterestGenericRegex();

    public static DetectedMediaUrl? TryDetect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var youtube = YouTubeRegex().Match(text);
        if (youtube.Success)
            return new DetectedMediaUrl(DetectedMediaPlatform.YouTube, youtube.Value);

        var pinIt = PinItRegex().Match(text);
        if (pinIt.Success)
            return new DetectedMediaUrl(DetectedMediaPlatform.Pinterest, pinIt.Value);

        var pin = PinterestPinRegex().Match(text);
        if (pin.Success)
            return new DetectedMediaUrl(DetectedMediaPlatform.Pinterest, pin.Value);

        var pinGeneric = PinterestGenericRegex().Match(text);
        if (pinGeneric.Success && pinGeneric.Value.Contains("/pin/", StringComparison.OrdinalIgnoreCase))
            return new DetectedMediaUrl(DetectedMediaPlatform.Pinterest, pinGeneric.Value);

        return null;
    }
}
