using System.Windows;

namespace vCamService.App;

public partial class MainWindow : Window
{
    private App AppInstance => (App)Application.Current;

    public MainWindow()
    {
        InitializeComponent();

        // Load settings into UI
        StreamUrlBox.Text = AppInstance.Settings.StreamUrl;
        AutoStartCheck.IsChecked = AppInstance.Settings.AutoStartOnBoot;

        // Subscribe to app events
        AppInstance.StatusChanged += RefreshStatus;
        AppInstance.LogAdded += OnLogAdded;

        // Show existing log history
        foreach (var msg in AppInstance.LogMessages)
            LogBox.AppendText(msg + "\n");
        LogBox.ScrollToEnd();

        RefreshStatus();

        // Hide on close instead of exit (tray app stays running)
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    private void RefreshStatus()
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"Status: {AppInstance.StatusText}";
            bool running = AppInstance.Manager?.IsRunning ?? false;
            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
            StreamUrlBox.IsEnabled = !running;
        });
    }

    private void OnLogAdded(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(message + "\n");
            LogBox.ScrollToEnd();
        });
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        string url = StreamUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            ErrorText.Text = "Please enter a stream URL";
            return;
        }
        AppInstance.StartStreamAsync(url);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        AppInstance.StopStream();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        AppInstance.Settings.StreamUrl = StreamUrlBox.Text.Trim();
        AppInstance.Settings.AutoStartOnBoot = AutoStartCheck.IsChecked ?? false;
        AppInstance.Settings.Save();
        ErrorText.Text = "";
        ErrorText.Foreground = System.Windows.Media.Brushes.Green;
        ErrorText.Text = "Settings saved ✓";
        _ = Task.Delay(2000).ContinueWith(_ =>
            Dispatcher.Invoke(() => { ErrorText.Text = ""; ErrorText.Foreground = System.Windows.Media.Brushes.Red; }));
    }
}
