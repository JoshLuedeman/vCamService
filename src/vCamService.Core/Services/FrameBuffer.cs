namespace vCamService.Core.Services;

/// <summary>
/// Thread-safe single-frame overwrite buffer.
/// Writers always overwrite the latest frame. Readers always get the freshest frame.
/// No backpressure — old frames are silently dropped.
/// </summary>
public sealed class FrameBuffer
{
    private byte[]? _frame;
    private int _width;
    private int _height;
    private readonly object _lock = new();

    public void Put(byte[] bgraData, int width, int height)
    {
        lock (_lock)
        {
            _frame = bgraData;
            _width = width;
            _height = height;
        }
    }

    public (byte[]? Data, int Width, int Height) Get()
    {
        lock (_lock)
        {
            return (_frame, _width, _height);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _frame = null;
            _width = 0;
            _height = 0;
        }
    }

    public bool HasFrame
    {
        get
        {
            lock (_lock) return _frame != null;
        }
    }
}
