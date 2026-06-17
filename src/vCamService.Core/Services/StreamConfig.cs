using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace vCamService.Core.Services;

/// <summary>
/// Stream configuration shared between the app (writer) and COM server (reader).
/// Stored at %ProgramData%\vCamService\stream-config.json.
/// The app writes this after probing the stream; the COM server reads it at activation.
/// </summary>
public sealed class StreamConfig
{
    private static readonly string DefaultConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "vCamService");
    private static readonly string DefaultConfigPath = Path.Combine(DefaultConfigDir, "stream-config.json");

    private readonly string _configDir;
    private readonly string _configPath;
    private readonly Action<string>? _logger;

    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int FpsNumerator { get; set; } = 30;
    public int FpsDenominator { get; set; } = 1;
    public int PixelFormat { get; set; } = SharedFrameBuffer.PixelFormatNV12;

    [JsonConstructor]
    public StreamConfig()
    {
        _configPath = DefaultConfigPath;
        _configDir = DefaultConfigDir;
        _logger = null;
    }

    public StreamConfig(string? configPath, Action<string>? logger)
    {
        _configPath = configPath ?? DefaultConfigPath;
        _configDir = Path.GetDirectoryName(_configPath) ?? DefaultConfigDir;
        _logger = logger;
    }

    public static StreamConfig Load(string? configPath = null, Action<string>? logger = null)
    {
        var config = new StreamConfig(configPath, logger);
        try
        {
            if (File.Exists(config._configPath))
            {
                string json = File.ReadAllText(config._configPath);
                var cfg = JsonSerializer.Deserialize<StreamConfig>(json);
                if (cfg is { Width: > 0, Height: > 0 })
                {
                    cfg._logger?.Invoke($"Loaded stream config: {cfg.Width}x{cfg.Height}");
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Failed to load stream config: {ex.Message}");
        }
        return config;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_configPath, JsonSerializer.Serialize(this, options));
            _logger?.Invoke($"Saved stream config: {Width}x{Height}");
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"CRITICAL: Failed to save stream config: {ex.Message}");
        }
    }
}
