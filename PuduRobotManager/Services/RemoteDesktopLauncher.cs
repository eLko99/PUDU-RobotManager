using System.Diagnostics;

namespace PuduRobotManager.Services;

public sealed class RemoteDesktopLauncher
{
    private readonly Func<string?> _configuredPath;

    public RemoteDesktopLauncher(Func<string?> configuredPath)
    {
        _configuredPath = configuredPath;
    }

    public string? ResolveExePath() => ToolLocator.FindScrcpy(_configuredPath());

    public void Launch()
    {
        var exePath = ResolveExePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException(
                "scrcpy.exe was not found. Place the scrcpy folder next to PuduRobotManager.exe, or set the path in Settings.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
        };

        Process.Start(startInfo);
    }
}
