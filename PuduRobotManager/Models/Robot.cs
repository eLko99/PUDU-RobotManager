using System.Text.Json.Serialization;

namespace PuduRobotManager.Models;

public sealed class Robot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; } = 5555;
    public string Notes { get; set; } = string.Empty;

    [JsonIgnore]
    public string Address => $"{Ip}:{Port}";
}
