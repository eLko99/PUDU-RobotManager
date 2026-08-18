namespace PuduRobotManager.Services;

internal static class ToolLocator
{
    private static readonly string[] ScrcpyRelativePaths =
    [
        Path.Combine("scrcpy", "scrcpy.exe"),
        Path.Combine("scrcpy", "scrcpy", "scrcpy.exe"),
        "scrcpy.exe",
    ];

    public static string? FindAdb(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return FindOnPath("adb.exe");
    }

    public static string? FindScrcpy(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var appDir = AppContext.BaseDirectory;
        foreach (var relative in ScrcpyRelativePaths)
        {
            var candidate = Path.Combine(appDir, relative);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
