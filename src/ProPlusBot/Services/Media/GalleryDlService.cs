using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class GalleryDlService(
    MediaToolsLocator tools,
    IOptions<DownloadOptions> options,
    ILogger<GalleryDlService> logger)
{
    private readonly DownloadOptions _options = options.Value;

    public async Task<MediaToolDownloadResult> DownloadAsync(string url, string outputDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        await tools.WaitReadyAsync(ct);
        if (!tools.HasGalleryDl)
            return MediaToolDownloadResult.Failed("gallery-dl is not installed or not ready on the server.");

        var result = await ProcessRunner.RunAsync(
            tools.GalleryDlPath!,
            ["--no-mtime", "-d", outputDirectory, url],
            outputDirectory,
            _options.ProcessTimeoutSeconds,
            ct);

        if (!result.Success)
        {
            logger.LogWarning("gallery-dl failed for {Url}: {Stderr}", url, result.StdErr);
            var detail = string.IsNullOrWhiteSpace(result.StdErr)
                ? $"gallery-dl exited with code {result.ExitCode}."
                : result.StdErr.Trim();
            return MediaToolDownloadResult.Failed(detail);
        }

        if (!Directory.Exists(outputDirectory))
            return MediaToolDownloadResult.Failed("gallery-dl produced no output directory.");

        var file = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();

        return file is null
            ? MediaToolDownloadResult.Failed("gallery-dl finished successfully but produced no file.")
            : MediaToolDownloadResult.Ok(file);
    }
}
