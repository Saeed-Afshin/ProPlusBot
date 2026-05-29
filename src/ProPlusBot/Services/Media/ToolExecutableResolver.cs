using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using SharpCompress.Compressors.Xz;

namespace ProPlusBot.Services.Media;

internal static class ToolExecutableResolver
{
    public static string ResolveToolsDirectory(IHostEnvironment hostEnvironment, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(hostEnvironment.ContentRootPath, configured);
        }

        return Path.Combine(hostEnvironment.ContentRootPath, "tools");
    }

    public static string ResolveMediaDirectory(IHostEnvironment hostEnvironment, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(hostEnvironment.ContentRootPath, configured);
        }

        return Path.Combine(Path.GetTempPath(), "ProPlusBot", "media");
    }

    public static async Task<string?> ResolveExecutableAsync(
        string? configuredPath,
        string bundledFullPath,
        string pathName,
        bool autoDownload,
        Func<Task<string?>> downloadAsync,
        ILogger logger,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = await TryResolveFileAsync(configuredPath, ct);
            if (configured is not null)
                return configured;

            logger.LogWarning(
                "Configured tool path {Path} is not usable; trying bundled install and PATH",
                configuredPath);
        }

        var bundled = await TryResolveFileAsync(bundledFullPath, ct);
        if (bundled is not null)
            return bundled;

        var onPath = await TryResolveFileAsync(pathName, ct);
        if (onPath is not null)
            return onPath;

        if (!autoDownload)
        {
            logger.LogInformation(
                "Auto-download disabled for {Tool}; bundled/path install not found",
                pathName);
            return null;
        }

        logger.LogInformation("Starting auto-download for {Tool}", pathName);
        var downloaded = await downloadAsync();
        if (downloaded is not null)
        {
            logger.LogInformation("Auto-download completed for {Tool}: {Path}", pathName, downloaded);
            return downloaded;
        }

        logger.LogError(
            "Auto-download failed for {Tool}. Check outbound HTTPS and write permissions for the tools directory.",
            pathName);

        return null;
    }

    public static async Task<string?> TryResolveFileAsync(string pathOrName, CancellationToken ct)
    {
        if (File.Exists(pathOrName))
            return Path.GetFullPath(pathOrName);

        if (!Path.IsPathRooted(pathOrName))
        {
            var onPath = await WhichAsync(pathOrName, ct);
            if (onPath is not null)
                return onPath;
        }

        return null;
    }

    public static async Task<string?> TryResolveDirectoryAsync(string pathOrName, CancellationToken ct)
    {
        if (File.Exists(pathOrName))
            return Path.GetDirectoryName(Path.GetFullPath(pathOrName));

        if (!Path.IsPathRooted(pathOrName))
        {
            var onPath = await WhichAsync(pathOrName, ct);
            if (onPath is not null)
                return Path.GetDirectoryName(onPath);
        }

        return null;
    }

    public static async Task DownloadFileAsync(
        HttpClient client,
        Uri url,
        string destinationPath,
        string toolName,
        ILogger logger,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length > 0)
        {
            logger.LogInformation(
                "{Tool} already exists at {Path}, skipping download",
                toolName,
                destinationPath);
            return;
        }

        logger.LogInformation(
            "Starting download for {Tool} from {Url} to {Path}",
            toolName,
            url,
            destinationPath);

        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(destinationPath);
            await stream.CopyToAsync(file, ct);

            var bytes = new FileInfo(destinationPath).Length;
            logger.LogInformation(
                "Download succeeded for {Tool}: {SizeBytes} bytes at {Path}",
                toolName,
                bytes,
                destinationPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Download failed for {Tool} from {Url}", toolName, url);
            throw;
        }
    }

    public static void MakeExecutable(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(filePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // best effort
        }
    }

    public static async Task ExtractArchiveAsync(
        string archivePath,
        string destination,
        FfmpegArchiveKind kind,
        string toolName,
        ILogger logger,
        CancellationToken ct)
    {
        var archiveBytes = File.Exists(archivePath) ? new FileInfo(archivePath).Length : 0;
        logger.LogInformation(
            "Starting extract for {Tool}: {Archive} ({ArchiveBytes} bytes, {Format}) -> {Destination}",
            toolName,
            archivePath,
            archiveBytes,
            kind,
            destination);

        try
        {
            if (kind == FfmpegArchiveKind.Zip)
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, destination), ct);
                logger.LogInformation("Extract succeeded for {Tool} using System.IO.Compression.Zip", toolName);
                return;
            }

            await ExtractTarXzAsync(archivePath, destination, toolName, logger, ct);
            logger.LogInformation("Extract succeeded for {Tool} from tar.xz archive", toolName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Extract failed for {Tool}: {Archive} -> {Destination}",
                toolName,
                archivePath,
                destination);
            throw;
        }
    }

    private static async Task ExtractTarXzAsync(
        string archivePath,
        string destination,
        string toolName,
        ILogger logger,
        CancellationToken ct)
    {
        Directory.CreateDirectory(destination);
        var errors = new List<string>();

        logger.LogInformation("Extract for {Tool}: trying streaming xz -> tar (no temp file)", toolName);
        if (await TryExtractTarXzStreamingAsync(archivePath, destination, toolName, logger, ct))
        {
            logger.LogInformation("Extract for {Tool}: streaming xz+tar succeeded", toolName);
            return;
        }

        errors.Add("streaming xz+tar extraction failed");

        logger.LogInformation("Extract for {Tool}: trying xz decompress to temp tar + TarFile", toolName);
        if (await TryExtractTarXzViaTempFileAsync(archivePath, destination, toolName, logger, ct))
        {
            logger.LogInformation("Extract for {Tool}: temp tar extraction succeeded", toolName);
            return;
        }

        errors.Add("temp tar extraction failed");

        logger.LogInformation("Extract for {Tool}: trying tar -xJf (timeout 15 min)", toolName);
        var tarJf = await ProcessRunner.RunAsync(
            "tar", ["-xJf", archivePath, "-C", destination], null, 900, ct);
        if (tarJf.Success)
        {
            logger.LogInformation("Extract for {Tool}: tar -xJf succeeded", toolName);
            return;
        }

        errors.Add($"tar -xJf: {tarJf.StdErr.Trim()}");
        logger.LogWarning(
            "Extract for {Tool}: tar -xJf failed (exit {ExitCode}): {StdErr}",
            toolName,
            tarJf.ExitCode,
            tarJf.StdErr.Trim());

        logger.LogInformation("Extract for {Tool}: trying xz -dc | tar -xf (timeout 15 min)", toolName);
        var tarXf = await ExtractTarAfterXzDecompressAsync(archivePath, destination, ct);
        if (tarXf)
        {
            logger.LogInformation("Extract for {Tool}: xz -dc | tar -xf succeeded", toolName);
            return;
        }

        errors.Add("xz -dc | tar -xf pipeline failed or xz is not installed");
        logger.LogError(
            "Extract for {Tool}: all tar.xz strategies failed. Attempts: {Attempts}",
            toolName,
            string.Join("; ", errors));

        throw new InvalidOperationException(
            $"Failed to extract {Path.GetFileName(archivePath)}. " +
            string.Join("; ", errors));
    }

    private static async Task<bool> TryExtractTarXzStreamingAsync(
        string archivePath,
        string destination,
        string toolName,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            logger.LogInformation(
                "Extract for {Tool}: TarFile.ExtractToDirectoryAsync on xz stream (no temp .tar file)",
                toolName);

            await using var input = File.OpenRead(archivePath);
            await using var xz = new XZStream(input);
            var extractTask = TarFile.ExtractToDirectoryAsync(
                xz,
                destination,
                overwriteFiles: true,
                cancellationToken: ct);
            await RunWithProgressHeartbeatAsync(extractTask, toolName, "tar stream extract", logger, ct);

            logger.LogInformation(
                "Extract for {Tool}: TarFile stream extract finished in {Elapsed}",
                toolName,
                sw.Elapsed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Extract for {Tool}: TarFile stream extract failed", toolName);
            return false;
        }
    }

    private static async Task RunWithProgressHeartbeatAsync(
        Task work,
        string toolName,
        string phase,
        ILogger logger,
        CancellationToken ct)
    {
        while (true)
        {
            var completed = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(10), ct));
            if (completed == work)
                break;

            logger.LogInformation(
                "Extract for {Tool}: {Phase} still in progress…",
                toolName,
                phase);
        }

        await work;
    }

    private static async Task<bool> TryExtractTarXzViaTempFileAsync(
        string archivePath,
        string destination,
        string toolName,
        ILogger logger,
        CancellationToken ct)
    {
        var tempTar = Path.Combine(Path.GetTempPath(), $"proplusbot-{Guid.NewGuid():N}.tar");
        try
        {
            logger.LogInformation(
                "Extract for {Tool}: decompressing xz to temp tar {TempTar}",
                toolName,
                tempTar);

            await using (var input = File.OpenRead(archivePath))
            await using (var xz = new XZStream(input))
            await using (var output = File.Create(tempTar))
            {
                await CopyStreamWithProgressAsync(xz, output, toolName, "xz decompress", logger, ct);
            }

            var tarBytes = new FileInfo(tempTar).Length;
            logger.LogInformation(
                "Extract for {Tool}: xz decompress done ({TarMegabytes:F1} MB), extracting tar to {Destination}",
                toolName,
                tarBytes / 1024.0 / 1024.0,
                destination);

            var extractSw = Stopwatch.StartNew();
            var extractTask = Task.Run(
                () => TarFile.ExtractToDirectory(tempTar, destination, overwriteFiles: true),
                ct);
            await RunWithProgressHeartbeatAsync(extractTask, toolName, "TarFile.ExtractToDirectory", logger, ct);
            logger.LogInformation(
                "Extract for {Tool}: TarFile.ExtractToDirectory finished in {Elapsed}",
                toolName,
                extractSw.Elapsed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Extract for {Tool}: temp tar extraction failed at {TempTar}",
                toolName,
                tempTar);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempTar))
                    File.Delete(tempTar);
            }
            catch
            {
                // best effort
            }
        }
    }

    private static async Task CopyStreamWithProgressAsync(
        Stream source,
        Stream destination,
        string toolName,
        string phase,
        ILogger logger,
        CancellationToken ct)
    {
        var buffer = new byte[1024 * 128];
        long total = 0;
        var sw = Stopwatch.StartNew();
        var lastLog = TimeSpan.Zero;

        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;

            if (sw.Elapsed - lastLog >= TimeSpan.FromSeconds(10))
            {
                logger.LogInformation(
                    "Extract for {Tool}: {Phase} — {Megabytes:F1} MB ({Elapsed})",
                    toolName,
                    phase,
                    total / 1024.0 / 1024.0,
                    sw.Elapsed);
                lastLog = sw.Elapsed;
            }
        }

        logger.LogInformation(
            "Extract for {Tool}: {Phase} complete — {Megabytes:F1} MB in {Elapsed}",
            toolName,
            phase,
            total / 1024.0 / 1024.0,
            sw.Elapsed);
    }

    private static async Task<bool> ExtractTarAfterXzDecompressAsync(
        string archivePath,
        string destination,
        CancellationToken ct)
    {
        var shell = await ProcessRunner.RunAsync(
            "sh",
            ["-c", $"xz -dc \"{archivePath.Replace("\"", "\\\"")}\" | tar -xf - -C \"{destination.Replace("\"", "\\\"")}\""],
            null,
            900,
            ct);

        return shell.Success;
    }

    private static async Task<string?> WhichAsync(string executable, CancellationToken ct)
    {
        var fileName = OperatingSystem.IsWindows() ? "where" : "which";
        var result = await ProcessRunner.RunAsync(fileName, [executable], null, 10, ct);
        if (!result.Success)
            return null;

        var line = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return line is not null && File.Exists(line) ? line : null;
    }
}
