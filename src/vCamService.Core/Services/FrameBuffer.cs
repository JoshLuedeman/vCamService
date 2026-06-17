namespace vCamService.Core.Services;

/// <summary>
/// Thread-safe single-frame overwrite buffer using a double-buffer strategy.
/// Writers alternate between two pre-allocated buffers to avoid per-frame allocation.
/// Readers always get the freshest frame. No backpressure — old frames are silently dropped.
/// </summary>
public sealed class FrameBuffer : IFrameBuffer
{
    private byte[]? _bufferA;
    private byte[]? _bufferB;
    private byte[]? _current;
    private int _width;
    private int _height;
    private readonly object _lock = new();

    /// <summary>
    /// Returns a write buffer of at least <paramref name="size"/> bytes.
    /// The caller should fill it with frame data, then call <see cref="Commit"/>.
    /// </summary>
    public byte[] GetWriteBuffer(int size)
    {
        lock (_lock)
        {
            // Pick the buffer that isn't currently the active read target
            ref byte[]? target = ref (_current == _bufferA) ? ref _bufferB : ref _bufferA;
            if (target == null || target.Length < size)
                target = new byte[size];
            return target;
        }
    }

    /// <summary>
    /// Marks the given buffer as the current frame and makes it available to readers.
    /// </summary>
    public void Commit(byte[] buffer, int width, int height)
    {
        lock (_lock)
        {
            _current = buffer;
            _width = width;
            _height = height;
        }
    }

    /// <summary>
    /// Puts frame data by copying into an internal buffer (allocation-free after warm-up).
    /// </summary>
    public void Put(byte[] bgraData, int width, int height)
    {
        var writeBuffer = GetWriteBuffer(bgraData.Length);
        Buffer.BlockCopy(bgraData, 0, writeBuffer, 0, bgraData.Length);
        Commit(writeBuffer, width, height);
    }

    public (byte[]? Data, int Width, int Height) Get()
    {
        lock (_lock)
        {
            return (_current, _width, _height);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _current = null;
            _width = 0;
            _height = 0;
        }
    }

    public bool HasFrame
    {
        get
        {
            lock (_lock) return _current != null;
        }
    }
}
