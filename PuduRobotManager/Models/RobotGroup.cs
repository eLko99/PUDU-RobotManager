namespace PuduRobotManager.Models;

public sealed class RobotGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
