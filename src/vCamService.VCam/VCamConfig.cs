namespace vCamService.VCam;

/// <summary>
/// Shared configuration for the virtual camera's resolution and frame rate.
/// Set by <see cref="VirtualCameraManager"/> before starting the camera;
/// read by <see cref="VirtualCameraSource"/> and <see cref="VirtualCameraStream"/>.
/// </summary>
public sealed record VCamConfig(int Width = 1280, int Height = 720, int Fps = 30)
{
    /// <summary>Frame duration in 100-nanosecond units (1 second / fps).</summary>
    public long FrameDuration100Ns => 10_000_000L / Fps;
}
