using System.Drawing;
using System.IO;
using System.Windows;
using H.NotifyIcon;
using vCamService.App.Services;
using vCamService.VCam;

namespace vCamService.App;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private VirtualCameraManager? _manager;
    private AppSettings _settings = new();
    private MainWindow? _settingsWindow;
    private readonly List<string> _logMessages = new();
    private const int MaxLogLines = 200;

    public VirtualCameraManager? Manager => _manager;
    public AppSettings Settings => _settings;
    public IReadOnlyList<string> LogMessages => _logMessages;

    public string StatusText { get; private set; } = "Stopped";
    public string TooltipText { get; private set; } = "vCamService — Stopped";

    public event Action? StatusChanged;
    public event Action<string>? LogAdded;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = AppSettings.Load();

        // Create tray icon
        var iconStream = GetResourceStream(new Uri("pack://application:,,,/Resources/camera.ico"))?.Stream;
        var icon = iconStream != null ? new Icon(iconStream) : SystemIcons.Application;

        _trayIcon = new TaskbarIcon
        {
            Icon = icon,
            ToolTipText = "vCamService — Starting…",
        };
        _trayIcon.TrayLeftMouseDown += (_, _) => ShowSettings();

        var menu = new System.Windows.Controls.ContextMenu();
        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings…" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var restartItem = new System.Windows.Controls.MenuItem { Header = "Restart Stream" };
        restartItem.Click += (_, _) => _ = RestartStreamAsync();
        menu.Items.Add(restartItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
        _trayIcon.ForceCreate();

        // Auto-start stream if URL is configured
        if (!string.IsNullOrWhiteSpace(_settings.StreamUrl))
        {
            _ = Task.Run(() =>
            {
                Thread.Sleep(500); // let tray icon render
                Dispatcher.BeginInvoke(() => StartStreamAsync(_settings.StreamUrl));
            });
        }
        else
        {
            ShowSettings();
        }
    }

    public async void StartStreamAsync(string url)
    {
        try
        {
            StopStream();
            _manager = new VirtualCameraManager();
            _manager.OnLog += msg => Dispatcher.BeginInvoke(() => AddLog(msg));
            _manager.OnError += msg => Dispatcher.BeginInvoke(() => AddLog($"ERROR: {msg}"));

            // Run the blocking Start (ffprobe + MF init) off the UI thread
            await Task.Run(() => _manager.Start(url));

            // Get detected resolution/fps from stream config (written during probe)
            string resolution = "Unknown";
            if (_manager is { IsRunning: true })
            {
                try
                {
                    var config = vCamService.Core.Services.StreamConfig.Load();
                    resolution = $"{config.Width}×{config.Height} @ {config.FpsNumerator}/{config.FpsDenominator} fps";
                }
                catch { }
            }

            StatusText = $"Streaming — {resolution}";
            TooltipText = $"vCamService — {resolution}";
            UpdateTray();
            AddLog($"Camera started: {resolution}");
        }
        catch (Exception ex)
        {
            StatusText = "Error";
            TooltipText = "vCamService — Error";
            UpdateTray();
            AddLog($"Start failed: {ex.Message}");
            _trayIcon?.ShowNotification("vCamService", $"Failed to start: {ex.Message}");
        }
        StatusChanged?.Invoke();
    }

    public void StopStream()
    {
        try
        {
            _manager?.Stop();
            _manager?.Dispose();
            _manager = null;
            StatusText = "Stopped";
            TooltipText = "vCamService — Stopped";
            UpdateTray();
            StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            AddLog($"Stop error: {ex.Message}");
        }
    }

    private async Task RestartStreamAsync()
    {
        StopStream();
        await Task.Delay(500);
        if (!string.IsNullOrWhiteSpace(_settings.StreamUrl))
            StartStreamAsync(_settings.StreamUrl);
    }

    private void UpdateTray()
    {
        if (_trayIcon != null)
            _trayIcon.ToolTipText = TooltipText;
    }

    private void AddLog(string message)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logMessages.Add(timestamped);
        while (_logMessages.Count > MaxLogLines)
            _logMessages.RemoveAt(0);
        LogAdded?.Invoke(timestamped);
    }

    public void ShowSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new MainWindow();
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void ExitApp()
    {
        try { StopStream(); } catch { }
        try { _trayIcon?.Dispose(); _trayIcon = null; } catch { }
        Dispatcher.InvokeAsync(() => Shutdown());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _manager?.Dispose();
        base.OnExit(e);
    }
}
