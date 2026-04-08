using System.Text.Json;
using vCamService.Core.Models;

namespace vCamService.Core.Services;

public class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _configPath;
    private readonly string _configDir;

    public ConfigService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "vCamService"))
    {
    }

    protected ConfigService(string configDir)
    {
        _configDir = configDir;
        _configPath = Path.Combine(_configDir, "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_configDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);

        // Atomic write: write to .tmp then move
        var tmpPath = _configPath + ".tmp";
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, _configPath, overwrite: true);
    }
}
