using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class MediaToolsBootstrapHostedService(
    IOptions<MediaDownloadOptions> options,
    IHostEnvironment hostEnvironment,
    MediaToolsLocator locator,
    IHttpClientFactory httpClientFactory,
    ILogger<MediaToolsBootstrapHostedService> logger) : IHostedService
{
    private readonly MediaDownloadOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var toolsDir = ToolExecutableResolver.ResolveToolsDirectory(hostEnvironment, _options.ToolsDirectory);

        var ytDlp = await ResolveYtDlpAsync(toolsDir, cancellationToken);
        var galleryDl = await ResolveGalleryDlAsync(toolsDir, cancellationToken);
        var ffmpegDir = await ResolveFfmpegAsync(toolsDir, cancellationToken);

        locator.Complete(ytDlp, galleryDl, ffmpegDir);

        if (locator.HasYtDlp || locator.HasGalleryDl)
        {
            logger.LogInformation(
                "Media tools ready — yt-dlp: {YtDlp}, gallery-dl: {GalleryDl}, ffmpeg dir: {Ffmpeg}",
                ytDlp ?? "(missing)",
                galleryDl ?? "(missing)",
                ffmpegDir ?? "(not available)");
        }
        else
        {
            logger.LogCritical(
                "Media downloads are disabled: neither yt-dlp nor gallery-dl is available. " +
                "Install tools on the server, set MediaDownload:YtDlpPath / GalleryDlPath, " +
                "or enable AutoDownloadYtDlp / AutoDownloadGalleryDl with outbound HTTPS and a writable tools directory ({ToolsDir}).",
                toolsDir);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private Task<string?> ResolveYtDlpAsync(string toolsDir, CancellationToken ct) =>
        ToolExecutableResolver.ResolveExecutableAsync(
            _options.YtDlpPath,
            Path.Combine(toolsDir, "yt-dlp", "bin", YtDlpDownloadAssets.InstalledName),
            YtDlpDownloadAssets.InstalledName,
            _options.AutoDownloadYtDlp,
            () => DownloadYtDlpAsync(toolsDir, ct),
            logger,
            ct);

    private Task<string?> ResolveGalleryDlAsync(string toolsDir, CancellationToken ct) =>
        ToolExecutableResolver.ResolveExecutableAsync(
            _options.GalleryDlPath,
            Path.Combine(toolsDir, "gallery-dl", "bin", GalleryDlDownloadAssets.InstalledName),
            GalleryDlDownloadAssets.InstalledName,
            _options.AutoDownloadGalleryDl,
            () => DownloadGalleryDlAsync(toolsDir, ct),
            logger,
            ct);

    private async Task<string?> ResolveFfmpegAsync(string toolsDir, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_options.FfmpegPath))
        {
            var configured = await ToolExecutableResolver.TryResolveDirectoryAsync(_options.FfmpegPath, ct);
            if (configured is not null)
                return configured;

            logger.LogWarning("Configured FfmpegPath {Path} is not usable", _options.FfmpegPath);
        }

        var bundled = await ToolExecutableResolver.TryResolveDirectoryAsync(
            Path.Combine(toolsDir, "ffmpeg", "bin", FfmpegPlatformAssets.ExecutableName), ct);
        if (bundled is not null)
            return bundled;

        var onPath = await ToolExecutableResolver.TryResolveDirectoryAsync(
            FfmpegPlatformAssets.ExecutableName, ct);
        if (onPath is not null)
            return onPath;

        if (!_options.AutoDownloadFfmpeg)
            return null;

        if (!FfmpegPlatformAssets.TryGetAsset(out var asset) || asset is null)
        {
            logger.LogWarning(
                "Automatic ffmpeg download is not supported on {OS}/{Arch}",
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture);
            return null;
        }

        return await DownloadFfmpegAsync(toolsDir, asset.Value, ct);
    }

    private async Task<string?> DownloadYtDlpAsync(string toolsDir, CancellationToken ct)
    {
        if (!YtDlpDownloadAssets.TryGetAsset(out var asset) || asset is null)
            return null;

        var installPath = Path.Combine(toolsDir, "yt-dlp", "bin", YtDlpDownloadAssets.InstalledName);
        logger.LogInformation("Downloading yt-dlp from {Url}", asset.Value.DownloadUrl);

        try
        {
            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(client, asset.Value.DownloadUrl, installPath, ct);
            ToolExecutableResolver.MakeExecutable(installPath);
            return await ToolExecutableResolver.TryResolveFileAsync(installPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download yt-dlp to {Path}", installPath);
            return null;
        }
    }

    private async Task<string?> DownloadGalleryDlAsync(string toolsDir, CancellationToken ct)
    {
        if (!GalleryDlDownloadAssets.TryGetAsset(out var asset) || asset is null)
            return null;

        var installPath = Path.Combine(toolsDir, "gallery-dl", "bin", GalleryDlDownloadAssets.InstalledName);
        logger.LogInformation("Downloading gallery-dl from {Url}", asset.Value.DownloadUrl);

        try
        {
            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(client, asset.Value.DownloadUrl, installPath, ct);
            ToolExecutableResolver.MakeExecutable(installPath);
            return await ToolExecutableResolver.TryResolveFileAsync(installPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download gallery-dl to {Path}", installPath);
            return null;
        }
    }

    private async Task<string?> DownloadFfmpegAsync(string toolsDir, FfmpegDownloadAsset asset, CancellationToken ct)
    {
        var downloadsDir = Path.Combine(toolsDir, "downloads");
        Directory.CreateDirectory(downloadsDir);

        var archivePath = Path.Combine(downloadsDir, asset.ArchiveFileName);
        var extractRoot = Path.Combine(toolsDir, ".ffmpeg-extract");
        var installPath = Path.Combine(toolsDir, "ffmpeg", "bin", FfmpegPlatformAssets.ExecutableName);

        try
        {
            if (Directory.Exists(extractRoot))
                Directory.Delete(extractRoot, recursive: true);

            logger.LogInformation("Downloading ffmpeg from {Url}", asset.DownloadUrl);

            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(client, asset.DownloadUrl, archivePath, ct);

            Directory.CreateDirectory(extractRoot);
            await ToolExecutableResolver.ExtractArchiveAsync(archivePath, extractRoot, asset.ArchiveKind, ct);

            var binary = Directory.EnumerateFiles(extractRoot, FfmpegPlatformAssets.ExecutableName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (binary is null)
            {
                logger.LogError("ffmpeg binary not found after extracting {Archive}", archivePath);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);
            File.Copy(binary, installPath, overwrite: true);
            ToolExecutableResolver.MakeExecutable(installPath);

            return await ToolExecutableResolver.TryResolveDirectoryAsync(installPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to download or extract ffmpeg (install ffmpeg on the server or set MediaDownload:FfmpegPath)");
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(extractRoot))
                    Directory.Delete(extractRoot, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not remove ffmpeg extract temp folder");
            }
        }
    }
}
