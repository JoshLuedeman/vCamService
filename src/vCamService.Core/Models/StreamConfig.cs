namespace vCamService.Core.Models;

public record StreamConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Protocol { get; init; } = "rtsp"; // "rtsp" | "mjpeg"
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public int Fps { get; init; } = 30;
    public string RtspTransport { get; init; } = "tcp"; // "tcp" | "udp"
    public bool Enabled { get; init; } = true;
}
