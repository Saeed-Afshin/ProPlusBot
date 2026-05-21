using System.Text.Json;

namespace ProPlusBot.Services.Media;

public class PinterestSearchService(
    HttpClient httpClient,
    ILogger<PinterestSearchService> logger)
{
    private const string SearchResourceUrl =
        "https://www.pinterest.com/resource/BaseSearchResource/get/";

    public async Task<PinterestSearchPage> SearchPageAsync(
        string query,
        string? bookmark,
        int pageSize,
        CancellationToken ct = default)
    {
        var requestPayload = JsonSerializer.Serialize(new
        {
            options = new { query, bookmarks = new[] { bookmark ?? "" } },
            context = new { }
        });

        var requestUri = $"{SearchResourceUrl}?data={Uri.EscapeDataString(requestPayload)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("X-Pinterest-AppState", "active");
        request.Headers.TryAddWithoutValidation("X-Pinterest-Source-Url", "/ideas/");
        request.Headers.TryAddWithoutValidation("X-Pinterest-PWS-Handler", "www/ideas.js");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Pinterest search HTTP request failed for query {Query}", query);
            return new PinterestSearchPage([], null);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Pinterest search returned {StatusCode} for query {Query}",
                response.StatusCode,
                query);
            return new PinterestSearchPage([], null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        pageSize = Math.Clamp(pageSize, 1, MediaConstants.MaxSearchResultsPerPage);
        var items = ParseResults(document.RootElement, pageSize);
        var nextBookmark = TryGetNextBookmark(document.RootElement);
        return new PinterestSearchPage(items, nextBookmark);
    }

    private static string? TryGetNextBookmark(JsonElement root)
    {
        if (!root.TryGetProperty("resource_response", out var resourceResponse)
            || !resourceResponse.TryGetProperty("bookmark", out var bookmarkElement))
        {
            return null;
        }

        if (bookmarkElement.ValueKind == JsonValueKind.Null)
            return null;

        var bookmark = bookmarkElement.GetString();
        return string.IsNullOrWhiteSpace(bookmark) ? null : bookmark;
    }

    private static IReadOnlyList<MediaSearchResultItem> ParseResults(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("resource_response", out var resourceResponse)
            || !resourceResponse.TryGetProperty("data", out var data)
            || !data.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<MediaSearchResultItem>();

        foreach (var result in results.EnumerateArray())
        {
            if (result.TryGetProperty("type", out var typeElement))
            {
                var type = typeElement.GetString();
                if (type is "story" or "board" or "user" or "article")
                    continue;
            }

            var link = ResolvePinterestPinUrl(result);
            if (link is null)
                continue;

            var title = result.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString()
                : null;

            title ??= result.TryGetProperty("grid_title", out var gridTitleElement)
                ? gridTitleElement.GetString()
                : null;

            items.Add(new MediaSearchResultItem(
                string.IsNullOrWhiteSpace(title) ? link : title,
                link,
                TryGetThumbnailUrl(result)));

            if (items.Count >= limit)
                break;
        }

        return items;
    }

    /// <summary>
    /// Pinterest search returns outbound destinations in <c>link</c> (Instagram, blogs, etc.).
    /// Downloads must use the pin URL built from <c>id</c> or a Pinterest-only fallback.
    /// </summary>
    private static string? ResolvePinterestPinUrl(JsonElement result)
    {
        if (result.TryGetProperty("id", out var idElement))
        {
            var pinId = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(pinId))
                return $"https://www.pinterest.com/pin/{pinId.Trim()}/";
        }

        foreach (var propertyName in new[] { "url", "link" })
        {
            if (!result.TryGetProperty(propertyName, out var urlElement))
                continue;

            var candidate = urlElement.GetString();
            if (IsPinterestPinUrl(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsPinterestPinUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var detected = MediaUrlDetector.TryDetect(url);
        return detected is { Platform: DetectedMediaPlatform.Pinterest };
    }

    private static string? TryGetThumbnailUrl(JsonElement result)
    {
        if (result.TryGetProperty("images", out var images))
        {
            foreach (var key in new[] { "orig", "736x", "474x", "236x", "170x" })
            {
                if (images.TryGetProperty(key, out var image)
                    && image.TryGetProperty("url", out var urlElement))
                {
                    var url = urlElement.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                        return url;
                }
            }
        }

        if (result.TryGetProperty("image_large_url", out var largeUrl))
        {
            var url = largeUrl.GetString();
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        return null;
    }
}
