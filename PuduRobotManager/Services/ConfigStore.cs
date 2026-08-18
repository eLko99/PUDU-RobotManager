using System.Text.Json;
using System.Text.Json.Serialization;
using PuduRobotManager.Models;

namespace PuduRobotManager.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string FilePath { get; }

    public ConfigStore()
        : this(Path.Combine(AppContext.BaseDirectory, "config.json"))
    {
    }

    public ConfigStore(string filePath)
    {
        FilePath = filePath;
    }

    public AppConfig Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppConfig();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            Normalize(config);
            return config;
        }
        catch (JsonException)
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    private static void Normalize(AppConfig config)
    {
        foreach (var group in config.Groups.Where(g => g.Id == Guid.Empty))
        {
            group.Id = Guid.NewGuid();
        }

        var groupIds = config.Groups.Select(g => g.Id).ToHashSet();
        foreach (var robot in config.Robots)
        {
            if (robot.Id == Guid.Empty)
            {
                robot.Id = Guid.NewGuid();
            }

            if (robot.GroupId is Guid groupId && !groupIds.Contains(groupId))
            {
                robot.GroupId = null;
            }
        }
    }
}
