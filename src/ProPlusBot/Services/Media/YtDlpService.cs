using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;

namespace ProPlusBot.Services.Media;

public class YtDlpService(
    IOptions<MediaDownloadOptions> options,
    YouTubeCookiesProvider cookiesProvider,
    MediaToolsLocator tools,
    ILogger<YtDlpService> logger)
{
    /// <summary>Order matters: tv/web_safari work best with account cookies; web/mweb often return storyboard-only without PO tokens.</summary>
    private static readonly string[] PlayerClientFallbacks =
    [
        "youtube:player_client=tv,web_safari",
        "youtube:player_client=tv",
        "youtube:player_client=tv_downgraded,web_safari",
        "youtube:player_client=android_vr",
        "youtube:player_client=web_safari",
        "youtube:player_client=web_creator",
        "youtube:player_client=default",
        "youtube:player_client=web",
        "youtube:player_client=android,web",
        "youtube:player_client=tv_embedded",
        "youtube:player_client=mweb"
    ];

    private static readonly Regex RealFormatRowPattern = new(
        @"^\s*\d{2,4}\s+(?:mp4|webm|m4a|3gp|mkv)\b",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly MediaDownloadOptions _options = options.Value;

    public async Task<IReadOnlyList<MediaSearchResultItem>> SearchYouTubeAsync(
        string query,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MediaConstants.MaxSearchResultsPerPage);
        var skip = pageIndex * pageSize;
        var fetchCount = skip + pageSize;
        var searchUrl = $"ytsearch{fetchCount}:{query}";
        await tools.WaitReadyAsync(ct);
        if (!tools.HasYtDlp)
            return [];

        var args = new List<string>
        {
            "--ignore-config",
            "--flat-playlist",
            "--no-warnings",
            "--print", "%(title)s)|%(id)s",
            searchUrl
        };
        await AddYouTubeArgsAsync(args, ct);

        var result = await ProcessRunner.RunAsync(
            tools.YtDlpPath!,
            args,
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

    public async Task<YouTubeFormatListResult> ListYouTubeFormatsAsync(string url, CancellationToken ct = default)
    {
        await tools.WaitReadyAsync(ct);
        if (!tools.HasYtDlp)
            return YouTubeFormatListResult.Failed("yt-dlp is not installed or not ready on the server.");

        var cookiesDiagnostic = await cookiesProvider.DescribeForErrorLogAsync(ct);
        FormatListParseDiagnostic? lastDiagnostic = null;
        ProcessResult? lastResult = null;
        var attemptLog = new StringBuilder();

        foreach (var playerClient in BuildPlayerClientAttempts())
        {
            foreach (var (buildArgs, mode) in new (Func<string, List<string>> Builder, string Mode)[]
                     {
                         (BuildFormatListJsonArgs, "json"),
                         (BuildFormatListTableArgs, "table")
                     })
            {
                var args = buildArgs(url);
                await AddYouTubeArgsAsync(args, ct, playerClient);

                var result = await ProcessRunner.RunAsync(
                    tools.YtDlpPath!,
                    args,
                    workingDirectory: null,
                    _options.ProcessTimeoutSeconds,
                    ct);
                lastResult = result;

                var parsed = TryParseFormatListFromProcess(result, mode, out var diagnostic);
                lastDiagnostic = diagnostic;
                attemptLog.Append(CultureInfo.InvariantCulture,
                    $"[{playerClient ?? "default"}/{mode}] exit={result.ExitCode} raw={diagnostic.RawFormatCount} storyboard={diagnostic.StoryboardFormatCount} selectable={diagnostic.SelectableFormatCount}; ");

                if (parsed is not null)
                    return parsed;

                logger.LogInformation(
                    "yt-dlp format list ({PlayerClient}/{Mode}) for {Url}: no selectable formats (exit {ExitCode}).",
                    playerClient ?? "default",
                    mode,
                    url,
                    result.ExitCode);
            }
        }

        logger.LogWarning(
            "yt-dlp format list failed for {Url} (exit {ExitCode}): {Stderr}",
            url,
            lastResult?.ExitCode,
            lastResult?.StdErr);

        var extraDiagnostics = await RunFormatListDiagnosticsAsync(url, ct);
        return YouTubeFormatListResult.Failed(
            BuildFormatListFailureDetail(
                lastResult,
                lastDiagnostic,
                cookiesDiagnostic,
                attemptLog.ToString(),
                extraDiagnostics));
    }

    private IEnumerable<string?> BuildPlayerClientAttempts()
    {
        var configured = _options.YouTubeExtractorArgs?.Trim();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(configured))
        {
            seen.Add(configured);
            yield return configured;
        }

        foreach (var fallback in PlayerClientFallbacks)
        {
            if (seen.Add(fallback))
                yield return fallback;
        }
    }

    /// <summary>Full JSON metadata — do not pass -f here (it can empty the formats array while still exiting 0).</summary>
    private static List<string> BuildFormatListJsonArgs(string url) =>
    [
        "--ignore-config",
        "--ignore-no-formats-error",
        "--no-playlist",
        "--no-download",
        "-J",
        url
    ];

    private static List<string> BuildFormatListTableArgs(string url) =>
    [
        "--ignore-config",
        "--ignore-no-formats-error",
        "--no-playlist",
        "--no-download",
        "-F",
        url
    ];

    private async Task<string> RunFormatListDiagnosticsAsync(string url, CancellationToken ct)
    {
        var sb = new StringBuilder();

        var verboseArgs = BuildFormatListTableArgs(url);
        verboseArgs.Add("--verbose");
        await AddYouTubeArgsAsync(verboseArgs, ct, "youtube:player_client=tv,web_safari");
        var verbose = await ProcessRunner.RunAsync(
            tools.YtDlpPath!,
            verboseArgs,
            workingDirectory: null,
            _options.ProcessTimeoutSeconds,
            ct);
        sb.AppendLine("--- yt-dlp --verbose -F (tv,web_safari, with cookies) ---");
        AppendProcessOutput(sb, verbose, stderrMax: 4000, stdoutMax: 1500);

        var noCookieArgs = BuildFormatListTableArgs(url);
        AddYouTubeRemoteComponents(noCookieArgs);
        SetExtractorArgs(noCookieArgs, "youtube:player_client=tv,web_safari");
        var noCookie = await ProcessRunner.RunAsync(
            tools.YtDlpPath!,
            noCookieArgs,
            workingDirectory: null,
            _options.ProcessTimeoutSeconds,
            ct);
        sb.AppendLine("--- yt-dlp -F (tv,web_safari, WITHOUT cookies) ---");
        AppendProcessOutput(sb, noCookie, stderrMax: 2000, stdoutMax: 1500);

        return sb.ToString().TrimEnd();
    }

    private static void AppendProcessOutput(StringBuilder sb, ProcessResult result, int stderrMax, int stdoutMax)
    {
        sb.AppendLine($"exit={result.ExitCode}");
        if (!string.IsNullOrWhiteSpace(result.StdErr))
            sb.AppendLine(Truncate(result.StdErr.Trim(), stderrMax));
        if (!string.IsNullOrWhiteSpace(result.StdOut))
            sb.AppendLine(Truncate(result.StdOut.Trim(), stdoutMax));
    }

    private static string Truncate(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen] + "…";

    private YouTubeFormatListResult? TryParseFormatListFromProcess(
        ProcessResult result,
        string mode,
        out FormatListParseDiagnostic diagnostic)
    {
        diagnostic = new FormatListParseDiagnostic(mode, 0, 0, 0, null);

        if (string.IsNullOrWhiteSpace(result.StdOut))
            return null;

        try
        {
            (List<YouTubeFormatOption> formats, int rawCount, int storyboardCount) = mode == "json"
                ? ParseFormatsFromJson(result.StdOut)
                : ParseFormatsFromListFormatsTable(result.StdOut);

            diagnostic = diagnostic with
            {
                RawFormatCount = rawCount,
                SelectableFormatCount = formats.Count,
                StoryboardFormatCount = storyboardCount
            };

            if (formats.Count > 0)
            {
                if (!result.Success)
                {
                    logger.LogWarning(
                        "yt-dlp format list ({Mode}) exited {ExitCode} but returned {Count} formats; using anyway. stderr: {Stderr}",
                        diagnostic.Mode,
                        result.ExitCode,
                        formats.Count,
                        result.StdErr);
                }

                return YouTubeFormatListResult.Ok(formats);
            }

            return null;
        }
        catch (Exception ex)
        {
            diagnostic = diagnostic with { ParseError = ex.Message };
            logger.LogWarning(ex, "Failed to parse yt-dlp format list ({Mode})", diagnostic.Mode);
            return null;
        }
    }

    private static string BuildFormatListFailureDetail(
        ProcessResult? result,
        FormatListParseDiagnostic? diagnostic,
        string cookiesDiagnostic,
        string attemptLog,
        string? extraDiagnostics = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(cookiesDiagnostic);

        if (!string.IsNullOrWhiteSpace(attemptLog))
            sb.AppendLine($"Attempts: {attemptLog.Trim()}");

        if (result is not { } run)
        {
            sb.AppendLine("yt-dlp did not run.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"yt-dlp exited with code {run.ExitCode} (mode: {diagnostic?.Mode ?? "unknown"}).");

        if (diagnostic is not null)
        {
            sb.AppendLine(
                $"Formats seen: {diagnostic.RawFormatCount}, storyboard-only: {diagnostic.StoryboardFormatCount}, selectable: {diagnostic.SelectableFormatCount}.");
            if (!string.IsNullOrWhiteSpace(diagnostic.ParseError))
                sb.AppendLine($"Parse error: {diagnostic.ParseError}");
        }

        var stdout = run.StdOut.Trim();
        if (StdoutIsStoryboardOnly(stdout))
        {
            sb.AppendLine(
                "YouTube returned ONLY storyboard rows (sb0–sb3, mhtml) — no video/audio streams. "
                + "Cookies may be present but rejected (expired, wrong export, or VPS IP differs from where cookies were created). "
                + "Re-export cookies on the same network as the server if possible, or use a PO token (MediaDownload:YouTubePoToken). "
                + "See verbose / no-cookie sections below and https://github.com/yt-dlp/yt-dlp/wiki/PO-Token-Guide");
        }
        else if (diagnostic is { RawFormatCount: > 0, SelectableFormatCount: 0 })
        {
            sb.AppendLine(
                "Formats were listed but none were selectable — refresh logged-in cookies in admin Settings.");
        }

        if (!string.IsNullOrWhiteSpace(run.StdErr))
            sb.AppendLine(run.StdErr.Trim());

        if (stdout.Length == 0)
            sb.AppendLine("stdout: (empty)");
        else
            sb.AppendLine($"stdout preview ({Math.Min(600, stdout.Length)} chars): {stdout[..Math.Min(600, stdout.Length)]}");

        if (!string.IsNullOrWhiteSpace(extraDiagnostics))
        {
            sb.AppendLine();
            sb.AppendLine(extraDiagnostics);
        }

        return sb.ToString().TrimEnd();
    }

    private static bool StdoutIsStoryboardOnly(string stdout) =>
        stdout.Contains("storyboard", StringComparison.OrdinalIgnoreCase)
        && !RealFormatRowPattern.IsMatch(stdout);

    private sealed record FormatListParseDiagnostic(
        string Mode,
        int RawFormatCount,
        int SelectableFormatCount,
        int StoryboardFormatCount,
        string? ParseError);

    public async Task<MediaToolDownloadResult> DownloadAsync(
        string url,
        string outputDirectory,
        string? youtubeFormatId = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputTemplate = Path.Combine(outputDirectory, "%(title).80B.%(ext)s");

        if (!IsYouTubeUrl(url))
            return await RunDownloadAsync(url, outputDirectory, outputTemplate, "best", ct);

        if (!string.IsNullOrWhiteSpace(youtubeFormatId))
            return await RunDownloadAsync(url, outputDirectory, outputTemplate, youtubeFormatId.Trim(), ct);

        var primary = await RunDownloadAsync(url, outputDirectory, outputTemplate, _options.YouTubeFormat, ct);
        if (primary.Success)
            return primary;

        logger.LogInformation("Retrying download as audio for {Url}", url);
        var audio = await RunDownloadAsync(url, outputDirectory, outputTemplate, _options.YouTubeAudioFormat, ct, extractAudio: true);
        if (audio.Success)
            return audio;

        return MediaToolDownloadResult.Failed(CombineAttemptErrors(primary.ErrorDetail, audio.ErrorDetail));
    }

    private static bool IsYouTubeUrl(string url) =>
        url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);

    private static string CombineAttemptErrors(string? primary, string? fallback)
    {
        var parts = new[] { primary, fallback }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return parts.Length == 0
            ? "yt-dlp download failed with no output."
            : string.Join(Environment.NewLine + "---" + Environment.NewLine, parts);
    }

    private async Task<MediaToolDownloadResult> RunDownloadAsync(
        string url,
        string outputDirectory,
        string outputTemplate,
        string format,
        CancellationToken ct,
        bool extractAudio = false)
    {
        var args = new List<string>
        {
            "--ignore-config",
            "--no-playlist",
            "--no-warnings",
            "-f", format,
            "-o", outputTemplate,
            url
        };

        if (extractAudio)
            args.AddRange(["-x", "--audio-format", "mp3"]);

        if (IsYouTubeUrl(url))
            await AddYouTubeArgsAsync(args, ct);

        await tools.WaitReadyAsync(ct);
        if (!tools.HasYtDlp)
            return MediaToolDownloadResult.Failed("yt-dlp is not installed or not ready on the server.");

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
            var detail = string.IsNullOrWhiteSpace(result.StdErr)
                ? $"yt-dlp exited with code {result.ExitCode}."
                : result.StdErr.Trim();
            return MediaToolDownloadResult.Failed(detail);
        }

        var file = FindLargestFile(outputDirectory);
        if (file is null)
        {
            return MediaToolDownloadResult.Failed(
                "yt-dlp finished successfully but produced no file in the output directory.");
        }

        return MediaToolDownloadResult.Ok(file);
    }

    private async Task AddYouTubeArgsAsync(
        List<string> args,
        CancellationToken ct,
        string? extractorArgsOverride = null)
    {
        AddYouTubeRemoteComponents(args);
        SetExtractorArgs(args, BuildExtractorArgString(extractorArgsOverride));

        var cookiesFile = await cookiesProvider.ResolveCookiesFileAsync(ct);
        if (cookiesFile is not null)
        {
            if (!File.Exists(cookiesFile))
            {
                logger.LogError("yt-dlp cookies file missing on disk: {CookiesFile}", cookiesFile);
            }
            else
            {
                logger.LogInformation(
                    "yt-dlp --cookies {CookiesFile} ({Bytes} bytes)",
                    cookiesFile,
                    new FileInfo(cookiesFile).Length);
            }

            args.Add("--cookies");
            args.Add(cookiesFile);
            return;
        }

        logger.LogWarning("yt-dlp YouTube request has no cookies file (admin panel, tools/, or env)");

        if (!string.IsNullOrWhiteSpace(_options.YouTubeCookiesFromBrowser))
        {
            args.Add("--cookies-from-browser");
            args.Add(_options.YouTubeCookiesFromBrowser.Trim());
            return;
        }

        if (YouTubeCookiesResolver.HasYouTubeAuth(_options))
            logger.LogWarning(
                "YouTube cookies are configured but could not be loaded. " +
                "Use a browser extension to export cookies.txt, place it at tools/{File}, " +
                "upload tools/{B64File}, or set YouTubeCookiesBase64File (avoid large inline env vars).",
                YouTubeCookiesResolver.DefaultCookiesFileName,
                YouTubeCookiesResolver.DefaultBase64FileName);
    }

    private string? BuildExtractorArgString(string? playerClientOverride) =>
        MergeYouTubeExtractorArgs(playerClientOverride ?? _options.YouTubeExtractorArgs, _options.YouTubePoToken);

    private static string? MergeYouTubeExtractorArgs(string? baseArgs, string? poToken)
    {
        var trimmedBase = baseArgs?.Trim();
        var trimmedPo = poToken?.Trim();
        if (string.IsNullOrEmpty(trimmedBase) && string.IsNullOrEmpty(trimmedPo))
            return null;

        if (string.IsNullOrEmpty(trimmedBase))
        {
            return trimmedPo!.StartsWith("youtube:", StringComparison.OrdinalIgnoreCase)
                ? trimmedPo
                : $"youtube:{trimmedPo}";
        }

        if (string.IsNullOrEmpty(trimmedPo))
            return trimmedBase;

        var poPart = trimmedPo.StartsWith("po_token=", StringComparison.OrdinalIgnoreCase)
            ? trimmedPo
            : $"po_token={trimmedPo}";

        var youtubePrefix = "youtube:";
        if (trimmedBase.StartsWith(youtubePrefix, StringComparison.OrdinalIgnoreCase))
            return $"{trimmedBase},{poPart}";

        return $"{youtubePrefix}{trimmedBase},{poPart}";
    }

    private void AddYouTubeRemoteComponents(List<string> args)
    {
        if (!string.IsNullOrWhiteSpace(_options.YouTubeRemoteComponents))
        {
            args.Add("--remote-components");
            args.Add(_options.YouTubeRemoteComponents.Trim());
        }

        var jsRuntimes = tools.JsRuntimesArg ?? _options.YouTubeJsRuntimes;
        if (!string.IsNullOrWhiteSpace(jsRuntimes))
        {
            args.Add("--js-runtimes");
            args.Add(jsRuntimes.Trim());
        }
    }

    private static void SetExtractorArgs(List<string> args, string? extractorArgs)
    {
        RemoveExtractorArgs(args);
        if (string.IsNullOrWhiteSpace(extractorArgs))
            return;

        args.Add("--extractor-args");
        args.Add(extractorArgs.Trim());
    }

    private static void RemoveExtractorArgs(List<string> args)
    {
        for (var i = args.Count - 1; i >= 0; i--)
        {
            if (args[i] == "--extractor-args" && i + 1 < args.Count)
            {
                args.RemoveAt(i + 1);
                args.RemoveAt(i);
            }
        }
    }

    private static (List<YouTubeFormatOption> Formats, int RawCount, int StoryboardCount) ParseFormatsFromJson(string stdout)
    {
        var json = ExtractJsonPayload(stdout);
        if (json is null)
            return ([], 0, 0);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
            return ParseFormatsArray(root);

        if (!root.TryGetProperty("formats", out var formatsElement)
            || formatsElement.ValueKind != JsonValueKind.Array)
        {
            return ([], 0, 0);
        }

        return ParseFormatsArray(formatsElement);
    }

    private static (List<YouTubeFormatOption> Formats, int RawCount, int StoryboardCount) ParseFormatsArray(
        JsonElement formatsElement)
    {
        var rawCount = formatsElement.GetArrayLength();
        var storyboardCount = 0;
        var parsed = new List<YouTubeFormatOption>();

        foreach (var element in formatsElement.EnumerateArray())
        {
            var formatId = GetString(element, "format_id");
            if (string.IsNullOrWhiteSpace(formatId))
                continue;

            if (IsStoryboardFormat(formatId, GetString(element, "ext"), element))
            {
                storyboardCount++;
                continue;
            }

            var vcodec = GetString(element, "vcodec") ?? "none";
            var acodec = GetString(element, "acodec") ?? "none";
            if (vcodec == "none" && acodec == "none")
            {
                storyboardCount++;
                continue;
            }

            parsed.Add(ToFormatOption(element, formatId, vcodec, acodec));
        }

        return (OrderAndCapFormats(parsed), rawCount, storyboardCount);
    }

    private static (List<YouTubeFormatOption> Formats, int RawCount, int StoryboardCount) ParseFormatsFromListFormatsTable(
        string stdout)
    {
        var rawCount = 0;
        var storyboardCount = 0;
        var parsed = new List<YouTubeFormatOption>();
        var inTable = false;

        foreach (var line in stdout.Split('\n'))
        {
            if (line.Contains("ID  EXT", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (!inTable || line.StartsWith('-') || string.IsNullOrWhiteSpace(line))
                continue;

            if (!ListFormatsRowPattern.IsMatch(line.Trim()))
                continue;

            rawCount++;
            var option = TryParseListFormatsLine(line);
            if (option is null)
            {
                storyboardCount++;
                continue;
            }

            parsed.Add(option);
        }

        return (OrderAndCapFormats(parsed), rawCount, storyboardCount);
    }

    private static YouTubeFormatOption? TryParseListFormatsLine(string line)
    {
        var left = line.Split('|', 2)[0].Trim();
        var match = ListFormatsLeftColumn.Match(left);
        if (!match.Success)
            return null;

        var formatId = match.Groups["id"].Value;
        var ext = match.Groups["ext"].Value;
        var resolution = match.Groups["resolution"].Value;

        if (IsStoryboardFormat(formatId, ext, resolution))
            return null;

        var audioOnly = resolution.Contains("audio only", StringComparison.OrdinalIgnoreCase);
        int? height = null;
        if (!audioOnly)
        {
            var resMatch = ResolutionPattern.Match(resolution);
            if (resMatch.Success)
                height = int.Parse(resMatch.Groups["height"].Value);
        }

        long? sizeBytes = null;
        if (line.Contains('|'))
        {
            var sizeMatch = FileSizePattern.Match(line);
            if (sizeMatch.Success)
                sizeBytes = ParseFileSize(sizeMatch);
        }

        var label = audioOnly
            ? "صوت"
            : height is > 0
                ? $"{height}p"
                : resolution.Trim();

        return new YouTubeFormatOption(formatId, label, ext, sizeBytes, height, audioOnly);
    }

    private static readonly Regex ListFormatsRowPattern = new(
        @"^\s*[\w+.-]+\s+\S+\s+.+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ListFormatsLeftColumn = new(
        @"^(?<id>[\w+.-]+)\s+(?<ext>\S+)\s+(?<resolution>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ResolutionPattern = new(
        @"(?<width>\d+)x(?<height>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FileSizePattern = new(
        @"(?<value>[\d.]+)\s*(?<unit>KiB|MiB|GiB|KB|MB|GB)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static bool IsStoryboardFormat(string formatId, string? ext, JsonElement element)
    {
        if (formatId.StartsWith("sb", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(ext, "mhtml", StringComparison.OrdinalIgnoreCase))
            return true;

        var note = GetString(element, "format_note") ?? "";
        return note.Contains("storyboard", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStoryboardFormat(string formatId, string ext, string resolution) =>
        formatId.StartsWith("sb", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ext, "mhtml", StringComparison.OrdinalIgnoreCase)
        || resolution.Contains("storyboard", StringComparison.OrdinalIgnoreCase);

    private static YouTubeFormatOption ToFormatOption(
        JsonElement element,
        string formatId,
        string vcodec,
        string acodec)
    {
        var ext = GetString(element, "ext") ?? "unknown";
        var note = GetString(element, "format_note")
            ?? GetString(element, "resolution")
            ?? formatId;
        var height = element.TryGetProperty("height", out var h) && h.TryGetInt32(out var hv) ? hv : (int?)null;
        var size = ResolveSizeBytes(element);
        var audioOnly = vcodec == "none" && acodec != "none";

        var label = audioOnly
            ? $"صوت {note}"
            : height is > 0
                ? $"{height}p {note}"
                : note;

        return new YouTubeFormatOption(formatId, label.Trim(), ext, size, height, audioOnly);
    }

    private static List<YouTubeFormatOption> OrderAndCapFormats(List<YouTubeFormatOption> parsed) =>
        parsed
            .GroupBy(f => f.FormatId)
            .Select(g => g.First())
            .OrderBy(f => f.IsAudioOnly)
            .ThenByDescending(f => f.Height ?? 0)
            .ThenByDescending(f => f.SizeBytes ?? 0)
            .Take(24)
            .ToList();

    private static string? ExtractJsonPayload(string stdout)
    {
        var trimmed = stdout.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed[0] is '{' or '[')
            return trimmed;

        var start = trimmed.IndexOf('{');
        if (start < 0)
            start = trimmed.IndexOf('[');

        if (start < 0)
            return null;

        var open = trimmed[start];
        var close = open == '{' ? '}' : ']';
        var end = trimmed.LastIndexOf(close);
        if (end <= start)
            return null;

        return trimmed[start..(end + 1)];
    }

    private static long? ParseFileSize(Match match)
    {
        if (!double.TryParse(
                match.Groups["value"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        var multiplier = unit switch
        {
            "KIB" or "KB" => 1024L,
            "MIB" or "MB" => 1024L * 1024,
            "GIB" or "GB" => 1024L * 1024 * 1024,
            _ => 1L
        };

        return (long)(value * multiplier);
    }

    private static long? ResolveSizeBytes(JsonElement element)
    {
        if (element.TryGetProperty("filesize", out var fs) && fs.TryGetInt64(out var exact) && exact > 0)
            return exact;

        if (element.TryGetProperty("filesize_approx", out var approx) && approx.TryGetInt64(out var approxBytes) && approxBytes > 0)
            return approxBytes;

        return null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? FindLargestFile(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        return Directory.EnumerateFiles(directory)
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
    }
}
