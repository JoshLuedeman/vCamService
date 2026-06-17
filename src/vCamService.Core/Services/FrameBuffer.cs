namespace vCamService.Core.Services;

/// <summary>
/// Thread-safe single-frame overwrite buffer using a triple-buffer strategy.
/// Three pre-allocated buffers ensure the writer never overwrites a buffer the
/// reader is still consuming, eliminating TOCTOU races without blocking either side.
/// No backpressure — old frames are silently dropped.
/// </summary>
public sealed class FrameBuffer : IFrameBuffer
{
    private byte[]?[] _buffers = new byte[]?[3];
    private int _readIndex = -1;  // index of latest committed frame (-1 = no frame)
    private int _writeIndex;
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
            // Pick any buffer that is not the current read target
            _writeIndex = (_readIndex + 1) % 3;
            if (_buffers[_writeIndex] == null || _buffers[_writeIndex]!.Length < size)
                _buffers[_writeIndex] = new byte[size];
            return _buffers[_writeIndex]!;
        }
    }

    /// <summary>
    /// Marks the given buffer as the current frame and makes it available to readers.
    /// </summary>
    public void Commit(byte[] buffer, int width, int height)
    {
        lock (_lock)
        {
            _readIndex = _writeIndex;
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
            if (_readIndex < 0)
                return (null, 0, 0);
            return (_buffers[_readIndex], _width, _height);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _readIndex = -1;
            _width = 0;
            _height = 0;
        }
    }

    public bool HasFrame
    {
        get
        {
            lock (_lock) return _readIndex >= 0;
        }
    }
}
