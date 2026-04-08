using vCamService.App.Services;

namespace vCamService.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ResourceMonitor _resourceMonitor = new();
    private DispatcherTimer? _statusTimer;

    public void StartResourceMonitoring()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += (_, _) =>
        {
            CpuPercent = _resourceMonitor.GetCpuPercent();
            RamMb = (long)_resourceMonitor.GetRamMb();
        };
        _statusTimer.Start();
    }


    [ObservableProperty] private ObservableCollection<StreamItemViewModel> _streams = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveStreamCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetActiveStreamCommand))]
    private StreamItemViewModel? _selectedStream;

    [ObservableProperty] private StreamItemViewModel? _activeStream;
    [ObservableProperty] private bool _isVCamRunning;
    [ObservableProperty] private string _vCamDeviceName = "vCamService Camera";
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private long _ramMb;
    [ObservableProperty] private double _currentFps;

    /// <summary>
    /// Set by the View to provide dialog-showing behaviour.
    /// Returns true if the user confirmed, false if cancelled.
    /// </summary>
    public Func<AddStreamViewModel, bool>? ShowAddStreamDialog { get; set; }

    /// <summary>Wired by App.xaml.cs to persist the new stream in the orchestrator.</summary>
    public Action<StreamConfig>? OnStreamAdded { get; set; }

    /// <summary>Wired by App.xaml.cs to stop and remove the stream reader.</summary>
    public Action<string>? OnStreamRemoved { get; set; }

    /// <summary>Wired by App.xaml.cs to update the active stream and preview source.</summary>
    public Action<string?>? ActiveStreamChangedCallback { get; set; }

    [RelayCommand]
    private void AddStream()
    {
        var vm = new AddStreamViewModel();
        bool confirmed = ShowAddStreamDialog?.Invoke(vm) ?? false;
        if (confirmed && vm.IsValid)
        {
            var config = vm.ToConfig();
            Streams.Add(StreamItemViewModel.FromConfig(config));
            OnStreamAdded?.Invoke(config);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveStream))]
    private void RemoveStream()
    {
        if (SelectedStream is null) return;
        var id = SelectedStream.Id;
        if (ActiveStream == SelectedStream) ActiveStream = null;
        Streams.Remove(SelectedStream);
        SelectedStream = null;
        OnStreamRemoved?.Invoke(id);
    }

    private bool CanRemoveStream() => SelectedStream is not null;

    [RelayCommand(CanExecute = nameof(CanSetActiveStream))]
    private void SetActiveStream()
    {
        if (SelectedStream is null) return;

        // De-activate the previous active stream
        if (ActiveStream is not null)
            ActiveStream.IsActive = false;

        ActiveStream = SelectedStream;
        ActiveStream.IsActive = true;
        ActiveStreamChangedCallback?.Invoke(ActiveStream.Id);
    }

    private bool CanSetActiveStream() => SelectedStream is not null;
}
