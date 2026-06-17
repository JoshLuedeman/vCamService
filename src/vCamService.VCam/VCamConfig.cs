namespace vCamService.VCam;

/// <summary>
/// Shared configuration for the virtual camera's resolution and frame rate.
/// Set by <see cref="VirtualCameraManager"/> before starting the camera;
/// read by <see cref="VirtualCameraSource"/> and <see cref="VirtualCameraStream"/>.
/// </summary>
public sealed record VCamConfig
{
    public int Width { get; }
    public int Height { get; }
    public int Fps { get; }

    public VCamConfig(int Width = 1280, int Height = 720, int Fps = 30)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Height, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Fps, 0);

        this.Width = Width;
        this.Height = Height;
        this.Fps = Fps;
    }

    /// <summary>Frame duration in 100-nanosecond units (1 second / fps).</summary>
    public long FrameDuration100Ns => 10_000_000L / Fps;
}
