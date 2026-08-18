using System.Diagnostics;
using System.Text;
using PuduRobotManager.Models;

namespace PuduRobotManager.Services;

public sealed class AdbService
{
    private readonly Func<string?> _configuredPath;

    public AdbService(Func<string?> configuredPath)
    {
        _configuredPath = configuredPath;
    }

    public string? ResolveAdbPath() => ToolLocator.FindAdb(_configuredPath());

    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("devices", TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Combined)
                ? "Failed to list ADB devices."
                : result.Combined);
        }

        return ParseDevices(result.Output);
    }

    public async Task<AdbCommandResult> ConnectAsync(Robot robot, CancellationToken cancellationToken = default)
    {
        var address = robot.Address;
        IReadOnlyList<AdbDevice> devices;
        try
        {
            devices = await GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return new AdbCommandResult
            {
                Success = false,
                Error = ex.Message,
                ExitCode = -1,
            };
        }

        foreach (var device in devices.Where(d => d.IsTcp && !SerialMatches(d.Serial, address)))
        {
            await RunAsync($"disconnect {device.Serial}", TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }

        var result = await RunAsync($"connect {address}", TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
        var combined = result.Combined;
        var connected = result.Success
            && (combined.Contains("connected to", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("already connected", StringComparison.OrdinalIgnoreCase));

        return new AdbCommandResult
        {
            Success = connected,
            Output = result.Output,
            Error = connected ? result.Error : (string.IsNullOrWhiteSpace(combined) ? $"Unable to connect to {address}." : combined),
            ExitCode = result.ExitCode,
        };
    }

    public Task<AdbCommandResult> DisconnectAllAsync(CancellationToken cancellationToken = default)
        => RunAsync("disconnect", TimeSpan.FromSeconds(10), cancellationToken);

    public static string StatusFor(Robot robot, IReadOnlyList<AdbDevice> devices)
    {
        var device = devices.FirstOrDefault(d => SerialMatches(d.Serial, robot.Address));
        if (device is null)
        {
            return "Disconnected";
        }

        return device.State.ToLowerInvariant() switch
        {
            "device" => "Connected",
            "offline" => "Offline",
            "unauthorized" => "Unauthorized",
            _ => device.State,
        };
    }

    public static bool IsConnected(Robot robot, IReadOnlyList<AdbDevice> devices)
        => devices.Any(d => SerialMatches(d.Serial, robot.Address) && d.IsReady);

    private async Task<AdbCommandResult> RunAsync(string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var adbPath = ResolveAdbPath();
        if (string.IsNullOrWhiteSpace(adbPath))
        {
            return new AdbCommandResult
            {
                Success = false,
                Error = "ADB was not found. Install Android platform-tools so adb is on PATH, or set the path in Settings.",
                ExitCode = -1,
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(adbPath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                return new AdbCommandResult
                {
                    Success = false,
                    Error = "Failed to start ADB.",
                    ExitCode = -1,
                };
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return new AdbCommandResult
                {
                    Success = false,
                    Output = stdout.ToString(),
                    Error = $"ADB timed out while running: adb {arguments}",
                    ExitCode = -1,
                };
            }

            return new AdbCommandResult
            {
                Success = process.ExitCode == 0,
                Output = stdout.ToString(),
                Error = stderr.ToString(),
                ExitCode = process.ExitCode,
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new AdbCommandResult
            {
                Success = false,
                Error = $"Failed to run ADB: {ex.Message}",
                ExitCode = -1,
            };
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static List<AdbDevice> ParseDevices(string output)
    {
        var devices = new List<AdbDevice>();
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0
                || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            devices.Add(new AdbDevice
            {
                Serial = parts[0],
                State = parts[1],
            });
        }

        return devices;
    }

    private static bool SerialMatches(string serial, string address)
        => string.Equals(serial, address, StringComparison.OrdinalIgnoreCase);
}
