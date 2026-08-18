namespace PuduRobotManager.Models;

public sealed class AppConfig
{
    public string AdbPath { get; set; } = string.Empty;
    public string RemoteDesktopExePath { get; set; } = string.Empty;
    public List<RobotGroup> Groups { get; set; } = [];
    public List<Robot> Robots { get; set; } = [];
}
