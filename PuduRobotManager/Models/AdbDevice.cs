namespace PuduRobotManager.Models;

public sealed class AdbDevice
{
    public string Serial { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;

    public bool IsTcp => Serial.Contains(':');
    public bool IsReady => string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);
}
