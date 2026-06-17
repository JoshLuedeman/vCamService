using System.IO;
using System.Text.Json;

namespace vCamService.App.Services;

/// <summary>
/// Persists user settings to %AppData%\vCamService\settings.json.
/// Manages the HKCU Run key for auto-start on boot.
/// </summary>
public sealed class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "vCamService");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyName = "vCamService";

    private readonly Action<string>? _logger;

    public string StreamUrl { get; set; } = "";
    public bool AutoStartOnBoot { get; set; }

    public AppSettings(Action<string>? logger = null)
    {
        _logger = logger;
    }

    public static AppSettings Load(Action<string>? logger = null)
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            logger?.Invoke($"Failed to load settings: {ex.Message}");
        }
        return new AppSettings(logger);
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, options));
            UpdateAutoStartRegistry();
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"Failed to save settings: {ex.Message}");
        }
    }

    private void UpdateAutoStartRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (AutoStartOnBoot)
            {
                string exePath = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(RunKeyName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(RunKeyName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"Failed to update auto-start registry: {ex.Message}");
        }
    }
}
