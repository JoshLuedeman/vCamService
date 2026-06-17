namespace vCamService.Core.Services;

/// <summary>
/// Abstraction for a thread-safe frame buffer that supports both
/// managed (Core) and COM interop (VCam) consumers.
/// </summary>
public interface IFrameBuffer
{
    void Put(byte[] bgraData, int width, int height);
    (byte[]? Data, int Width, int Height) Get();
    void Clear();
    bool HasFrame { get; }
}
