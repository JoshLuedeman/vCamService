using System.IO;
using System.Text.Json;

namespace vCamService.Core.Services;

/// <summary>
/// Stream configuration shared between the app (writer) and COM server (reader).
/// Stored at %ProgramData%\vCamService\stream-config.json.
/// The app writes this after probing the stream; the COM server reads it at activation.
/// </summary>
public sealed class StreamConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "vCamService");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "stream-config.json");

    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int FpsNumerator { get; set; } = 30;
    public int FpsDenominator { get; set; } = 1;
    public int PixelFormat { get; set; } = SharedFrameBuffer.PixelFormatNV12;

    public static StreamConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<StreamConfig>(json);
                if (cfg is { Width: > 0, Height: > 0 }) return cfg;
            }
        }
        catch { }
        return new StreamConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, options));
        }
        catch { }
    }
}
