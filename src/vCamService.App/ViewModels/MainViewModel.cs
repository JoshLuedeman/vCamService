namespace vCamService.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
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

    [RelayCommand]
    private void AddStream()
    {
        var vm = new AddStreamViewModel();
        bool confirmed = ShowAddStreamDialog?.Invoke(vm) ?? false;
        if (confirmed && vm.IsValid)
        {
            Streams.Add(new StreamItemViewModel
            {
                Name = vm.Name,
                Url  = vm.Url
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveStream))]
    private void RemoveStream()
    {
        if (SelectedStream is null) return;
        if (ActiveStream == SelectedStream) ActiveStream = null;
        Streams.Remove(SelectedStream);
        SelectedStream = null;
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
    }

    private bool CanSetActiveStream() => SelectedStream is not null;
}
