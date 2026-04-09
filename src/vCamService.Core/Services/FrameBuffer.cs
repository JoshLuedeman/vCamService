namespace vCamService.Core.Services;

/// <summary>
/// Thread-safe frame buffer for passing BGRA frames between the WPF app
/// and the virtual camera COM source. Future FFmpeg/RTSP integration will
/// write frames here.
/// </summary>
public class FrameBuffer
{
    private readonly object _lock = new();
    private byte[]? _data;
    private int _width;
    private int _height;

    public void Put(byte[] bgraData, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bgraData);
        lock (_lock)
        {
            _data = bgraData;
            _width = width;
            _height = height;
        }
    }

    public bool TryGet(out byte[] data, out int width, out int height)
    {
        lock (_lock)
        {
            if (_data == null)
            {
                data = [];
                width = 0;
                height = 0;
                return false;
            }
            data = _data;
            width = _width;
            height = _height;
            return true;
        }
    }
}
