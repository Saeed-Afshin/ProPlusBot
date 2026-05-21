using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class YtDlpService(
    IOptions<MediaDownloadOptions> options,
    MediaToolsLocator tools,
    ILogger<YtDlpService> logger)
{
    private readonly MediaDownloadOptions _options = options.Value;

    public async Task<IReadOnlyList<MediaSearchResultItem>> SearchYouTubeAsync(
        string query,
        int pageIndex = 0,
        CancellationToken ct = default)
    {
        var pageSize = MediaConstants.SearchResultsPerPage;
        var skip = pageIndex * pageSize;
        var fetchCount = skip + pageSize;
        var searchUrl = $"ytsearch{fetchCount}:{query}";
        await tools.WaitReadyAsync(ct);
        if (!tools.HasYtDlp)
            return [];

        var result = await ProcessRunner.RunAsync(
            tools.YtDlpPath!,
            [
                "--flat-playlist",
                "--no-warnings",
                "--print", "%(title)s)|%(id)s",
                searchUrl
            ],
            workingDirectory: null,
            _options.ProcessTimeoutSeconds,
            ct);

        if (!result.Success)
        {
            logger.LogWarning("yt-dlp YouTube search failed: {Stderr}", result.StdErr);
            return [];
        }

        var items = new List<MediaSearchResultItem>();
        foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var sep = line.LastIndexOf('|');
            if (sep <= 0)
                continue;

            var title = line[..sep].Trim();
            var id = line[(sep + 1)..].Trim();
            if (string.IsNullOrEmpty(id))
                continue;

            items.Add(new MediaSearchResultItem(
                string.IsNullOrEmpty(title) ? id : title,
                $"https://www.youtube.com/watch?v={id}",
                $"https://i.ytimg.com/vi/{id}/hqdefault.jpg"));
        }

        return items.Skip(skip).Take(pageSize).ToList();
    }

    public async Task<string?> DownloadAsync(string url, string outputDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputTemplate = Path.Combine(outputDirectory, "%(title).80B.%(ext)s");

        if (!IsYouTubeUrl(url))
            return await RunDownloadAsync(url, outputDirectory, outputTemplate, "best", ct);

        var primary = await RunDownloadAsync(url, outputDirectory, outputTemplate, _options.YouTubeFormat, ct);
        if (primary is not null)
            return primary;

        logger.LogInformation("Retrying download as audio for {Url}", url);
        return await RunDownloadAsync(url, outputDirectory, outputTemplate, _options.YouTubeAudioFormat, ct, extractAudio: true);
    }

    private static bool IsYouTubeUrl(string url) =>
        url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> RunDownloadAsync(
        string url,
        string outputDirectory,
        string outputTemplate,
        string format,
        CancellationToken ct,
        bool extractAudio = false)
    {
        var args = new List<string>
        {
            "--no-playlist",
            "--no-warnings",
            "-f", format,
            "-o", outputTemplate,
            url
        };

        if (extractAudio)
            args.AddRange(["-x", "--audio-format", "mp3"]);

        await tools.WaitReadyAsync(ct);
        if (!tools.HasYtDlp)
            return null;

        if (IsYouTubeUrl(url) && tools.FfmpegDirectory is not null)
        {
            args.Insert(0, tools.FfmpegDirectory);
            args.Insert(0, "--ffmpeg-location");
        }

        var result = await ProcessRunner.RunAsync(
            tools.YtDlpPath!,
            args,
            outputDirectory,
            _options.ProcessTimeoutSeconds,
            ct);

        if (!result.Success)
        {
            logger.LogWarning("yt-dlp download failed for {Url}: {Stderr}", url, result.StdErr);
            return null;
        }

        return FindLargestFile(outputDirectory);
    }

    private static string? FindLargestFile(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        return Directory.EnumerateFiles(directory)
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
    }
}
