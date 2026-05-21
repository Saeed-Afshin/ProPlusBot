using System.Text.Json;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class PinterestSearchService(
    HttpClient httpClient,
    IOptions<MediaDownloadOptions> options,
    ILogger<PinterestSearchService> logger)
{
    private const string SearchResourceUrl =
        "https://www.pinterest.com/resource/BaseSearchResource/get/";

    private readonly MediaDownloadOptions _options = options.Value;

    public async Task<IReadOnlyList<MediaSearchResultItem>> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        var requestPayload = JsonSerializer.Serialize(new
        {
            options = new { query, bookmarks = new[] { "" } },
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
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Pinterest search returned {StatusCode} for query {Query}",
                response.StatusCode,
                query);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        return ParseResults(document.RootElement, _options.MaxSearchResults);
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
            if (result.TryGetProperty("type", out var typeElement)
                && typeElement.GetString() == "story")
            {
                continue;
            }

            var pinId = result.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            string? link = null;
            if (result.TryGetProperty("link", out var linkElement))
                link = linkElement.GetString();

            link ??= pinId is not null
                ? $"https://www.pinterest.com/pin/{pinId}/"
                : null;

            if (string.IsNullOrWhiteSpace(link))
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
