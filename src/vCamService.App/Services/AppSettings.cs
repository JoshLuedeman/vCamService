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

    public string StreamUrl { get; set; } = "";
    public bool AutoStartOnBoot { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
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
        catch { }
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
        catch { }
    }
}
