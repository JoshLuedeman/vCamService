using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ConfigService>? _logger;

    public ConfigService(ILogger<ConfigService>? logger = null)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "vCamService"), logger)
    {
    }

    protected ConfigService(string configDir, ILogger<ConfigService>? logger = null)
    {
        _configDir = configDir;
        _configPath = Path.Combine(_configDir, "config.json");
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load config from {ConfigPath}, using defaults", _configPath);
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
