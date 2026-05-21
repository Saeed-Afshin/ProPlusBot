using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class GalleryDlService(
    MediaToolsLocator tools,
    IOptions<MediaDownloadOptions> options,
    ILogger<GalleryDlService> logger)
{
    private readonly MediaDownloadOptions _options = options.Value;

    public async Task<string?> DownloadAsync(string url, string outputDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        await tools.WaitReadyAsync(ct);
        if (!tools.HasGalleryDl)
            return null;

        var result = await ProcessRunner.RunAsync(
            tools.GalleryDlPath!,
            ["--no-mtime", "-d", outputDirectory, url],
            outputDirectory,
            _options.ProcessTimeoutSeconds,
            ct);

        if (!result.Success)
        {
            logger.LogWarning("gallery-dl failed for {Url}: {Stderr}", url, result.StdErr);
            return null;
        }

        if (!Directory.Exists(outputDirectory))
            return null;

        return Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
    }
}
