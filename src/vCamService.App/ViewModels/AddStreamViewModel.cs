namespace vCamService.App.ViewModels;

public partial class AddStreamViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _url = string.Empty;

    [ObservableProperty] private string _protocol = "rtsp";
    [ObservableProperty] private string _rtspTransport = "tcp";
    [ObservableProperty] private int _width = 1280;
    [ObservableProperty] private int _height = 720;
    [ObservableProperty] private int _fps = 30;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        (Url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
         Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> ProtocolOptions { get; } = new[] { "rtsp", "mjpeg" };
    public static IEnumerable<string> TransportOptions { get; } = new[] { "tcp", "udp" };

    public StreamConfig ToConfig() => new StreamConfig
    {
        Id = Guid.NewGuid().ToString(),
        Name = Name,
        Url = Url,
        Protocol = Protocol,
        RtspTransport = RtspTransport,
        Width = Width,
        Height = Height,
        Fps = Fps,
        Enabled = true
    };
}
