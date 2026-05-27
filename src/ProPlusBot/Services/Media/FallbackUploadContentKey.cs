using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace ProPlusBot.Services.Media;

public static partial class FallbackUploadContentKey
{
    public static string Compute(string sourceUrl, DetectedMediaPlatform platform, string? youTubeFormatId)
    {
        var normalizedUrl = NormalizeSourceUrl(sourceUrl, platform);
        var format = string.IsNullOrWhiteSpace(youTubeFormatId) ? "" : youTubeFormatId.Trim();
        var payload = $"{(int)platform}|{normalizedUrl}|{format}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeSourceUrl(string url, DetectedMediaPlatform platform)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return trimmed;

        if (platform == DetectedMediaPlatform.YouTube)
        {
            var videoId = TryExtractYouTubeVideoId(uri);
            if (!string.IsNullOrEmpty(videoId))
                return $"youtube:{videoId}";
        }

        var builder = new UriBuilder(uri) { Fragment = "" };
        if (string.IsNullOrEmpty(builder.Query))
            return builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');

        var query = QueryHelpers.ParseQuery(builder.Query);
        var keysToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
            "fbclid", "si", "feature", "pp"
        };
        var filtered = query
            .Where(p => !keysToRemove.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        builder.Query = filtered.Count == 0
            ? ""
            : QueryString.Create(filtered).ToString();
        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string? TryExtractYouTubeVideoId(Uri uri)
    {
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri.AbsolutePath.Trim('/').Split('/')[0];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        if (!uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Contains("youtu", StringComparison.OrdinalIgnoreCase))
            return null;

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (query.TryGetValue("v", out var vValues))
        {
            var v = vValues.ToString();
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        var pathMatch = YouTubePathVideoId().Match(uri.AbsolutePath);
        return pathMatch.Success ? pathMatch.Groups[1].Value : null;
    }

    [GeneratedRegex(@"/(?:embed|v|shorts|live)/([^/?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubePathVideoId();
}
