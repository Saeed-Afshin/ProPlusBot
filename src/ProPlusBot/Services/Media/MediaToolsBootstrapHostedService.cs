using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class MediaToolsBootstrapHostedService(
    IOptions<DownloadOptions> options,
    IHostEnvironment hostEnvironment,
    MediaToolsLocator locator,
    YouTubeCookiesProvider cookiesProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<MediaToolsBootstrapHostedService> logger) : IHostedService
{
    private readonly DownloadOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var toolsDir = ToolExecutableResolver.ResolveToolsDirectory(hostEnvironment, _options.ToolsDirectory);
        var mediaDir = ToolExecutableResolver.ResolveMediaDirectory(hostEnvironment, _options.MediaDirectory);
        logger.LogInformation(
            "Media tools bootstrap starting (tools: {ToolsDir}, media downloads: {MediaDir})",
            toolsDir,
            mediaDir);

        var ytDlp = await ResolveYtDlpAsync(toolsDir, cancellationToken);
        var galleryDl = await ResolveGalleryDlAsync(toolsDir, cancellationToken);
        var ffmpegDir = await ResolveFfmpegAsync(toolsDir, cancellationToken);
        var jsRuntimesArg = await ResolveJsRuntimesArgAsync(toolsDir, cancellationToken);
        var denoPath = ExtractDenoPathFromJsRuntimesArg(jsRuntimesArg);

        locator.Complete(ytDlp, galleryDl, ffmpegDir, jsRuntimesArg, denoPath);

        if (locator.HasYtDlp)
        {
            var cookies = await cookiesProvider.ResolveCookiesFileAsync(cancellationToken);
            var hasAdmin = await cookiesProvider.HasAdminCookiesAsync(cancellationToken);

            if (cookies is null && !hasAdmin && !YouTubeCookiesResolver.HasYouTubeAuth(_options))
            {
                logger.LogWarning(
                    "YouTube downloads will likely fail until cookies are configured. " +
                    "Paste cookies in admin Settings, or use tools/{File} / Download env vars.",
                    YouTubeCookiesResolver.DefaultCookiesFileName);
            }
            else if (cookies is not null)
            {
                logger.LogInformation(
                    "YouTube cookies ready ({Source}): {CookiesFile}",
                    hasAdmin ? "admin panel" : "configuration",
                    cookies);
            }

            await LogYtDlpVersionAsync(ytDlp!, cancellationToken);
            await LogYouTubeJsRuntimeAsync(cancellationToken);
        }

        if (locator.HasYtDlp || locator.HasGalleryDl)
        {
            logger.LogInformation(
                "Media tools ready — yt-dlp: {YtDlp}, gallery-dl: {GalleryDl}, ffmpeg dir: {Ffmpeg}, deno: {Deno}",
                ytDlp ?? "(missing)",
                galleryDl ?? "(missing)",
                ffmpegDir ?? "(not available)",
                denoPath ?? "(not available)");
        }
        else
        {
            logger.LogCritical(
                "Media downloads are disabled: neither yt-dlp nor gallery-dl is available. " +
                "Install tools on the server, set Download:YtDlpPath / GalleryDlPath, " +
                "or enable AutoDownloadYtDlp / AutoDownloadGalleryDl with outbound HTTPS and a writable tools directory ({ToolsDir}).",
                toolsDir);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task LogYouTubeJsRuntimeAsync(CancellationToken ct)
    {
        var jsArg = locator.JsRuntimesArg;
        if (string.IsNullOrWhiteSpace(jsArg))
        {
            logger.LogWarning(
                "YouTube JS runtime not available — enable Download:AutoDownloadDeno and ensure tools/ is writable, " +
                "or set Download:YouTubeDenoPath. See scripts/youtube-server-setup.md");
            return;
        }

        var executable = jsArg.Contains(':', StringComparison.Ordinal)
            ? jsArg[(jsArg.IndexOf(':') + 1)..]
            : jsArg;

        try
        {
            var result = await ProcessRunner.RunAsync(
                executable,
                ["--version"],
                workingDirectory: null,
                timeoutSeconds: 15,
                ct);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StdOut))
            {
                logger.LogInformation("YouTube JS runtime ({Arg}): {Version}", jsArg, result.StdOut.Trim());
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to query Deno/JS runtime version at {Path}", executable);
        }

        logger.LogWarning(
            "YouTube JS runtime not executable at {Path} — YouTube may return storyboard-only formats.",
            executable);
    }

    private static string? ExtractDenoPathFromJsRuntimesArg(string? jsRuntimesArg)
    {
        if (string.IsNullOrWhiteSpace(jsRuntimesArg))
            return null;

        var colon = jsRuntimesArg.IndexOf(':');
        if (colon <= 0)
            return null;

        var kind = jsRuntimesArg[..colon];
        return kind.Equals("deno", StringComparison.OrdinalIgnoreCase)
            ? jsRuntimesArg[(colon + 1)..]
            : null;
    }

    private async Task<string?> ResolveJsRuntimesArgAsync(string toolsDir, CancellationToken ct)
    {
        var configured = _options.YouTubeJsRuntimes?.Trim();
        if (!string.IsNullOrEmpty(configured) && configured.Contains(':', StringComparison.Ordinal))
            return configured;

        var denoPath = await ResolveDenoExecutableAsync(toolsDir, ct);
        if (denoPath is not null)
            return $"deno:{denoPath}";

        if (!string.IsNullOrEmpty(configured))
            return configured;

        return null;
    }

    private Task<string?> ResolveDenoExecutableAsync(string toolsDir, CancellationToken ct) =>
        ToolExecutableResolver.ResolveExecutableAsync(
            _options.YouTubeDenoPath,
            Path.Combine(toolsDir, "deno", "bin", DenoDownloadAssets.InstalledName),
            DenoDownloadAssets.InstalledName,
            _options.AutoDownloadDeno,
            () => DownloadDenoAsync(toolsDir, ct),
            logger,
            ct);

    private async Task<string?> DownloadDenoAsync(string toolsDir, CancellationToken ct)
    {
        if (!DenoDownloadAssets.TryGetAsset(out var asset) || asset is null)
        {
            logger.LogWarning(
                "Automatic Deno download is not supported on {OS}/{Arch}",
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture);
            return null;
        }

        var installPath = Path.Combine(toolsDir, "deno", "bin", DenoDownloadAssets.InstalledName);
        var existingDeno = await ToolExecutableResolver.TryResolveFileAsync(installPath, ct);
        if (existingDeno is not null)
        {
            logger.LogInformation("deno already installed at {Path}, skipping download", existingDeno);
            return existingDeno;
        }

        var downloadsDir = Path.Combine(toolsDir, "downloads");
        var archivePath = Path.Combine(downloadsDir, asset.Value.ArchiveFileName);
        var extractRoot = Path.Combine(toolsDir, ".deno-extract");

        try
        {
            if (Directory.Exists(extractRoot))
                Directory.Delete(extractRoot, recursive: true);

            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(
                client, asset.Value.DownloadUrl, archivePath, "deno", logger, ct);

            logger.LogInformation(
                "Starting extract for deno: {Archive} -> {Destination}",
                archivePath,
                extractRoot);
            Directory.CreateDirectory(extractRoot);
            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractRoot), ct);
                logger.LogInformation("Extract succeeded for deno (zip)");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Extract failed for deno: {Archive} -> {Destination}", archivePath, extractRoot);
                throw;
            }

            var binary = Directory
                .EnumerateFiles(extractRoot, asset.Value.ExecutableName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (binary is null)
            {
                logger.LogError(
                    "Install failed for deno: binary {Name} not found under {ExtractRoot}",
                    asset.Value.ExecutableName,
                    extractRoot);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);
            File.Copy(binary, installPath, overwrite: true);
            ToolExecutableResolver.MakeExecutable(installPath);
            logger.LogInformation("Installed deno at {Path} (from {Source})", installPath, binary);

            return await ToolExecutableResolver.TryResolveFileAsync(installPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download or extract deno to {Path}", installPath);
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
                logger.LogDebug(ex, "Could not remove deno extract temp folder");
            }
        }
    }

    private async Task LogYtDlpVersionAsync(string ytDlpPath, CancellationToken ct)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                ytDlpPath,
                ["--version"],
                workingDirectory: null,
                timeoutSeconds: 30,
                ct);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StdOut))
                logger.LogInformation("yt-dlp version: {Version}", result.StdOut.Trim());
            else
                logger.LogWarning("Could not read yt-dlp version (exit {Code})", result.ExitCode);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to query yt-dlp --version");
        }
    }

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
            {
                logger.LogInformation("Using ffmpeg from configured FfmpegPath: {Path}", configured);
                return configured;
            }

            logger.LogWarning("Configured FfmpegPath {Path} is not usable", _options.FfmpegPath);
        }

        var bundled = await ToolExecutableResolver.TryResolveDirectoryAsync(
            Path.Combine(toolsDir, "ffmpeg", "bin", FfmpegPlatformAssets.ExecutableName), ct);
        if (bundled is not null)
        {
            logger.LogInformation("Using bundled ffmpeg at {Path}", bundled);
            return bundled;
        }

        var onPath = await ToolExecutableResolver.TryResolveDirectoryAsync(
            FfmpegPlatformAssets.ExecutableName, ct);
        if (onPath is not null)
        {
            logger.LogInformation("Using ffmpeg from system PATH: {Path}", onPath);
            return onPath;
        }

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
        var existing = await ToolExecutableResolver.TryResolveFileAsync(installPath, ct);
        if (existing is not null)
        {
            logger.LogInformation("yt-dlp already installed at {Path}, skipping download", existing);
            return existing;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(
                client, asset.Value.DownloadUrl, installPath, "yt-dlp", logger, ct);
            ToolExecutableResolver.MakeExecutable(installPath);
            logger.LogInformation("Installed yt-dlp at {Path}", installPath);
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
        var existing = await ToolExecutableResolver.TryResolveFileAsync(installPath, ct);
        if (existing is not null)
        {
            logger.LogInformation("gallery-dl already installed at {Path}, skipping download", existing);
            return existing;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(
                client, asset.Value.DownloadUrl, installPath, "gallery-dl", logger, ct);
            ToolExecutableResolver.MakeExecutable(installPath);
            logger.LogInformation("Installed gallery-dl at {Path}", installPath);
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
        var existingFfmpeg = await ToolExecutableResolver.TryResolveDirectoryAsync(installPath, ct);
        if (existingFfmpeg is not null)
        {
            logger.LogInformation("ffmpeg already installed at {Path}, skipping download", existingFfmpeg);
            return existingFfmpeg;
        }

        try
        {
            if (Directory.Exists(extractRoot))
                Directory.Delete(extractRoot, recursive: true);

            var client = httpClientFactory.CreateClient(nameof(MediaToolsBootstrapHostedService));
            await ToolExecutableResolver.DownloadFileAsync(
                client, asset.DownloadUrl, archivePath, "ffmpeg", logger, ct);

            Directory.CreateDirectory(extractRoot);
            await ToolExecutableResolver.ExtractArchiveAsync(
                archivePath, extractRoot, asset.ArchiveKind, "ffmpeg", logger, ct);

            var binary = Directory.EnumerateFiles(extractRoot, FfmpegPlatformAssets.ExecutableName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (binary is null)
            {
                var fileCount = Directory.Exists(extractRoot)
                    ? Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).Count()
                    : 0;
                logger.LogError(
                    "Install failed for ffmpeg: {Name} not found under {ExtractRoot} ({FileCount} files extracted from {Archive})",
                    FfmpegPlatformAssets.ExecutableName,
                    extractRoot,
                    fileCount,
                    archivePath);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);
            File.Copy(binary, installPath, overwrite: true);
            ToolExecutableResolver.MakeExecutable(installPath);
            logger.LogInformation("Installed ffmpeg at {Path} (from {Source})", installPath, binary);

            return await ToolExecutableResolver.TryResolveDirectoryAsync(installPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to download or extract ffmpeg (install ffmpeg on the server or set Download:FfmpegPath)");
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
