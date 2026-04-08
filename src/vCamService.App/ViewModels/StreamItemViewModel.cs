namespace vCamService.App.ViewModels;

public partial class StreamItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private StreamStatus _status = StreamStatus.Disconnected;
    [ObservableProperty] private bool _isActive;

    public string Id { get; init; } = Guid.NewGuid().ToString();

    public StreamConfig ToConfig() => new StreamConfig
    {
        Id = Id,
        Name = Name,
        Url = Url,
        Enabled = true
    };

    public static StreamItemViewModel FromConfig(StreamConfig c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Url = c.Url
    };
}
