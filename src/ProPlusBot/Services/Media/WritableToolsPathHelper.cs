namespace ProPlusBot.Services.Media;

internal static class WritableToolsPathHelper
{
    /// <summary>
    /// Picks a directory under tools (or temp) that accepts new files.
    /// Kubernetes often mounts only subfolders like tools/downloads as writable.
    /// </summary>
    public static string Resolve(string preferredToolsDir, ILogger logger)
    {
        var candidates = new[]
        {
            Path.Combine(preferredToolsDir, "downloads"),
            preferredToolsDir,
            Path.Combine(Path.GetTempPath(), "ProPlusBot")
        };

        foreach (var dir in candidates)
        {
            if (TryProbeWrite(dir))
            {
                if (!string.Equals(dir, preferredToolsDir, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "Using writable cookies directory {Dir} (configured tools root: {ToolsRoot})",
                        dir,
                        preferredToolsDir);
                }

                return dir;
            }

            logger.LogDebug("Directory not writable for cookie file: {Dir}", dir);
        }

        throw new IOException(
            $"No writable directory for YouTube cookies under {preferredToolsDir} or temp.");
    }

    private static bool TryProbeWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
