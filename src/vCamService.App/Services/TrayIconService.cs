using H.NotifyIcon;

namespace vCamService.App.Services;

/// <summary>
/// Manages the system-tray icon using H.NotifyIcon.Wpf.
/// Call Initialize(mainWindow) once on startup; call Dispose() on shutdown.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "vCamService"
            // Icon / IconSource: Phase 4 TODO — wire up app-icon.ico from Resources/
        };

        var contextMenu = new ContextMenu();

        var showItem = new MenuItem { Header = "Show Window" };
        showItem.Click += (_, _) => ShowWindow();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new Separator());

        var quitItem = new MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => Application.Current.Shutdown();
        contextMenu.Items.Add(quitItem);

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
