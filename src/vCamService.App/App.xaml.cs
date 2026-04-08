using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using vCamService.App.Services;
using vCamService.App.ViewModels;
using vCamService.App.Views;
using vCamService.Core.Services;
using vCamService.VCam;

namespace vCamService.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "vCamService", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "vcamservice-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        DispatcherUnhandledException += (s, ex) =>
        {
            Log.Error(ex.Exception, "Unhandled dispatcher exception");
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            Log.Fatal(ex.ExceptionObject as Exception, "Unhandled domain exception");
        };
        TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            Log.Error(ex.Exception, "Unobserved task exception");
            ex.SetObserved();
        };

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfigService, ConfigService>();
                    services.AddSingleton<VirtualCameraManager>();
                    services.AddSingleton<AppOrchestrator>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<TrayIconService>();
                })
                .Build();

            await _host.StartAsync();

            if (!FfmpegChecker.IsAvailable())
            {
                MessageBox.Show(
                    FfmpegChecker.GetInstallInstructions(),
                    "FFmpeg Not Found — vCamService",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                // Don't shutdown — user might fix it and retry, or it may be in a custom path
            }

            var orchestrator = _host.Services.GetRequiredService<AppOrchestrator>();
            var mainVm       = _host.Services.GetRequiredService<MainViewModel>();
            var mainWindow   = _host.Services.GetRequiredService<MainWindow>();
            var tray         = _host.Services.GetRequiredService<TrayIconService>();

            // Wire the Add-stream dialog so the ViewModel can show it without
            // depending directly on the View layer.
            mainVm.ShowAddStreamDialog = vm =>
            {
                var dlg = new AddStreamDialog(vm) { Owner = mainWindow };
                return dlg.ShowDialog() == true;
            };

            // Wire stream-lifecycle commands to the orchestrator.
            mainVm.OnStreamAdded = async cfg => await orchestrator.AddStreamAsync(cfg);
            mainVm.OnStreamRemoved = id => orchestrator.RemoveStream(id);
            mainVm.ActiveStreamChangedCallback = id =>
            {
                orchestrator.SetActiveStream(id);
                var reader = orchestrator.GetReader(id ?? "");
                mainWindow.UpdatePreviewSource(reader?.FrameBuffer);
            };

            // Populate UI with streams that already exist in the saved config.
            var config = _host.Services.GetRequiredService<IConfigService>().Load();
            foreach (var stream in config.Streams)
                mainVm.Streams.Add(StreamItemViewModel.FromConfig(stream));

            // Start the virtual camera and all enabled stream readers.
            await orchestrator.StartAsync();

            // Wire preview to the initial active stream.
            if (config.ActiveStreamId != null)
            {
                var reader = orchestrator.GetReader(config.ActiveStreamId);
                mainWindow.UpdatePreviewSource(reader?.FrameBuffer);

                var activeItem = mainVm.Streams.FirstOrDefault(s => s.Id == config.ActiveStreamId);
                if (activeItem != null) activeItem.IsActive = true;
            }

            mainVm.StartResourceMonitoring();
            mainWindow.Show();
            tray.Initialize(mainWindow);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed");
            MessageBox.Show($"Startup failed: {ex.Message}", "vCamService", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var orchestrator = _host?.Services.GetService<AppOrchestrator>();
        if (orchestrator != null) await orchestrator.ShutdownAsync();
        if (_host != null) await _host.StopAsync();
        _host?.Dispose();
        base.OnExit(e);
        Log.CloseAndFlush();
    }
}
