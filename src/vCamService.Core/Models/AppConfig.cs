namespace vCamService.Core.Models;

public record AppConfig
{
    public int ConfigVersion { get; init; } = 1;
    public string? ActiveStreamId { get; init; }
    public int VCamWidth { get; init; } = 1280;
    public int VCamHeight { get; init; } = 720;
    public int VCamFps { get; init; } = 30;
    public bool MinimizeToTray { get; init; } = true;
    public List<StreamConfig> Streams { get; init; } = new();
}
