using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;

namespace ProPlusBot.Services.Media;

/// <summary>
/// Resolves YouTube cookies for yt-dlp. Admin cookies are read from DB and written under /tmp (always writable in containers).
/// </summary>
public class YouTubeCookiesProvider(
    IServiceScopeFactory scopeFactory,
    IOptions<DownloadOptions> options,
    IHostEnvironment hostEnvironment,
    ILogger<YouTubeCookiesProvider> logger)
{
    public const string AdminCookiesFileName = "youtube-cookies.active.txt";

    private readonly DownloadOptions _options = options.Value;
    private readonly string _adminCookiePath = Path.Combine(
        Path.GetTempPath(),
        "ProPlusBot",
        "youtube-cookies",
        AdminCookiesFileName);

    public async Task<string?> ResolveCookiesFileAsync(CancellationToken ct = default)
    {
        var fromAdmin = await TryMaterializeAdminCookiesAsync(ct);
        if (fromAdmin.Success && fromAdmin.Path is not null)
            return fromAdmin.Path;

        if (fromAdmin.Error is not null)
            logger.LogWarning("Admin YouTube cookies not used: {Reason}", fromAdmin.Error);

        return YouTubeCookiesResolver.ResolveFromConfiguration(_options, hostEnvironment, logger);
    }

    public async Task<bool> HasAdminCookiesAsync(CancellationToken ct = default) =>
        !string.IsNullOrWhiteSpace(await ReadAdminContentAsync(ct));

    public async Task<string> DescribeForErrorLogAsync(CancellationToken ct = default)
    {
        var content = await ReadAdminContentAsync(ct);
        var hasDb = !string.IsNullOrWhiteSpace(content);
        var hasLogin = hasDb && YouTubeCookiesContentParser.HasLoggedInSessionCookies(content!);
        var youtubeLines = hasDb
            ? content!.Split('\n').Count(l => l.Contains(".youtube.com", StringComparison.OrdinalIgnoreCase))
            : 0;
        var fileNote = File.Exists(_adminCookiePath)
            ? $"{new FileInfo(_adminCookiePath).Length} bytes"
            : "missing";

        return $"Admin cookies in DB: {(hasDb ? "yes" : "no")}; login markers (SID/LOGIN_INFO): {(hasLogin ? "yes" : "no")}; youtube.com lines: {youtubeLines}; active file: {_adminCookiePath} ({fileNote})";
    }

    /// <summary>Writes DB cookies to /tmp and verifies the file exists. Call after admin save.</summary>
    public async Task<(bool Success, string? Path, string? Error)> TryMaterializeAdminCookiesAsync(
        CancellationToken ct = default)
    {
        var content = await ReadAdminContentAsync(ct);
        if (string.IsNullOrWhiteSpace(content))
            return (false, null, "No YouTube cookies in BotSettings (Id=1).");

        try
        {
            var directory = Path.GetDirectoryName(_adminCookiePath)!;
            Directory.CreateDirectory(directory);

            var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            await File.WriteAllTextAsync(_adminCookiePath, normalized, ct);

            if (!File.Exists(_adminCookiePath))
                return (false, null, $"File was not created at {_adminCookiePath}");

            var length = new FileInfo(_adminCookiePath).Length;
            if (length == 0)
                return (false, null, "Cookie file is empty after write.");

            var hasLogin = YouTubeCookiesContentParser.HasLoggedInSessionCookies(normalized);
            logger.LogInformation(
                "YouTube admin cookies materialized at {Path} ({Bytes} bytes, loggedIn={HasLogin})",
                _adminCookiePath,
                length,
                hasLogin);

            if (!hasLogin)
            {
                logger.LogWarning(
                    "YouTube cookies lack SID/LOGIN_INFO — export while signed in to Google, not incognito-only.");
            }

            return (true, _adminCookiePath, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to materialize admin YouTube cookies at {Path}", _adminCookiePath);
            return (false, null, ex.Message);
        }
    }

    private async Task<string?> ReadAdminContentAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BotSettings
            .AsNoTracking()
            .Where(s => s.Id == 1)
            .Select(s => s.YouTubeCookiesContent)
            .FirstOrDefaultAsync(ct);
    }
}
