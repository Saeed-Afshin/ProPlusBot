using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

/// <summary>
/// Resolves YouTube cookies without requiring yt-dlp --cookies-from-browser on the server.
/// </summary>
public static class YouTubeCookiesResolver
{
    public const string DefaultCookiesFileName = "youtube-cookies.txt";
    public const string GeneratedCookiesFileName = "youtube-cookies.generated.txt";
    public const string DefaultBase64FileName = "youtube-cookies.b64.txt";

    public static string? ResolveFromConfiguration(
        MediaDownloadOptions options,
        IHostEnvironment hostEnvironment,
        ILogger? logger = null)
    {
        var toolsDir = ToolExecutableResolver.ResolveToolsDirectory(hostEnvironment, options.ToolsDirectory);

        if (TryResolveConfiguredPath(options.YouTubeCookiesFile, toolsDir, out var configured))
            return configured;

        var defaultPath = Path.Combine(toolsDir, DefaultCookiesFileName);
        if (File.Exists(defaultPath))
            return defaultPath;

        var fromBase64File = TryLoadCookiesFromBase64Source(
            ReadBase64FromConfiguredFile(options.YouTubeCookiesBase64File, toolsDir, logger),
            "YouTubeCookiesBase64File",
            toolsDir,
            logger);
        if (fromBase64File is not null)
            return fromBase64File;

        var defaultB64Path = Path.Combine(toolsDir, DefaultBase64FileName);
        if (File.Exists(defaultB64Path))
        {
            var fromDefaultB64 = TryLoadCookiesFromBase64Source(
                File.ReadAllText(defaultB64Path).Trim(),
                DefaultBase64FileName,
                toolsDir,
                logger);
            if (fromDefaultB64 is not null)
                return fromDefaultB64;
        }

        if (!string.IsNullOrWhiteSpace(options.YouTubeCookiesBase64))
        {
            var fromInline = TryLoadCookiesFromBase64Source(
                options.YouTubeCookiesBase64.Trim(),
                "YouTubeCookiesBase64",
                toolsDir,
                logger);
            if (fromInline is not null)
                return fromInline;
        }

        return null;
    }

    public static bool HasYouTubeAuth(MediaDownloadOptions options) =>
        !string.IsNullOrWhiteSpace(options.YouTubeCookiesFromBrowser)
        || !string.IsNullOrWhiteSpace(options.YouTubeCookiesFile)
        || !string.IsNullOrWhiteSpace(options.YouTubeCookiesBase64File)
        || !string.IsNullOrWhiteSpace(options.YouTubeCookiesBase64);

    private static string? ReadBase64FromConfiguredFile(
        string? configuredPath,
        string toolsDir,
        ILogger? logger)
    {
        if (!TryResolveConfiguredPath(configuredPath, toolsDir, out var fullPath) || fullPath is null)
            return null;

        try
        {
            return File.ReadAllText(fullPath).Trim();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to read YouTube Base64 cookies file {Path}", fullPath);
            return null;
        }
    }

    private static string? TryLoadCookiesFromBase64Source(
        string? base64,
        string sourceName,
        string toolsDir,
        ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(base64);
            var writableDir = WritableToolsPathHelper.Resolve(toolsDir, logger ?? NullLogger.Instance);
            var generated = Path.Combine(writableDir, GeneratedCookiesFileName);
            File.WriteAllBytes(generated, bytes);
            logger?.LogInformation("YouTube cookies loaded from {Source} into {Path}", sourceName, generated);
            return generated;
        }
        catch (FormatException ex)
        {
            logger?.LogError(ex, "Invalid Base64 in {Source}", sourceName);
            return null;
        }
    }

    private static bool TryResolveConfiguredPath(string? configuredPath, string toolsDir, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(configuredPath))
            return false;

        fullPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(toolsDir, configuredPath));

        return File.Exists(fullPath);
    }
}
