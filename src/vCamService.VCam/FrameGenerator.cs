using System.Runtime.InteropServices;
using DirectN;
using vCamService.Core.Services;
using vCamService.VCam.Utilities;

namespace vCamService.VCam;

/// <summary>
/// Frame generator that reads NV12 frames from shared memory and delivers them directly
/// to the allocator sample. The COM server owns the Global\ shared memory buffer —
/// created at source activation time using dimensions from StreamConfig.
/// Falls back to a black test pattern when no live frame is available.
/// </summary>
public class FrameGenerator : IDisposable
{
    private bool _disposed;
    private ulong _frameCount;
    private SharedFrameBuffer? _sharedBuffer;
    private bool _hasLiveFrame;
    private int _sharedPixelFormat = SharedFrameBuffer.PixelFormatNV12;

    public ulong FrameCount => _frameCount;

    // Not used in NV12-native path; kept for potential future GPU converter
    public void SetD3DManager(object manager, uint width, uint height) { }
    public void EnsureConverter(uint width, uint height) { }

    /// <summary>
    /// Create the shared memory buffer at source activation time (early, not lazy).
    /// </summary>
    public void CreateBuffer(StreamConfig? config)
    {
        if (_sharedBuffer != null) return;
        if (config == null || config.Width <= 0 || config.Height <= 0) return;

        try
        {
            _sharedBuffer = SharedFrameBuffer.CreateOwner(
                config.Width, config.Height,
                config.FpsNumerator, config.FpsDenominator,
                config.PixelFormat);
            _sharedPixelFormat = config.PixelFormat;
            EventProvider.LogInfo($"SharedFrameBuffer created: {config.Width}x{config.Height} " +
                $"@ {config.FpsNumerator}/{config.FpsDenominator} fps, pixfmt={config.PixelFormat}");
        }
        catch (Exception ex)
        {
            EventProvider.LogError($"Failed to create shared buffer: {ex.Message}");
        }
    }

    /// <summary>
    /// Fill the allocator sample with live stream data (NV12 or RGB32).
    /// </summary>
    public IComObject<IMFSample> Generate(IComObject<IMFSample> sample, Guid format, uint width, uint height)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sample);

            int frameSize;
            if (format == MFConstants.MFVideoFormat_NV12)
                frameSize = (int)(width * height * 3 / 2);
            else
                frameSize = (int)(width * height * 4);

            // NV12→NV12: direct copy (zero conversion — fast path!)
            // Shared memory has NV12 data, output wants NV12 — just memcpy.
            if (format == MFConstants.MFVideoFormat_NV12 && _sharedPixelFormat == SharedFrameBuffer.PixelFormatNV12)
            {
                return DirectCopy(sample, width, height, frameSize);
            }

            // RGB32 requested but shared memory has NV12 — fallback to green
            // (Rare: most apps prefer NV12. Full NV12→RGB conversion could be added later.)
            return FillFallback(sample, format, width, height, frameSize);
        }
        catch (Exception e)
        {
            EventProvider.LogError(e.ToString());
            throw;
        }
    }

    /// <summary>
    /// Fast path: read NV12 from shared memory directly into allocator buffer.
    /// No per-frame cache copy — only caches on torn reads (rare).
    /// </summary>
    private IComObject<IMFSample> DirectCopy(IComObject<IMFSample> sample, uint width, uint height, int frameSize)
    {
        using var buffer = sample.GetBufferByIndex(0);

        buffer.WithLock((scanline, length, _) =>
        {
            bool gotFrame = false;

            if (_sharedBuffer != null)
            {
                for (int retry = 0; retry < 3 && !gotFrame; retry++)
                {
                    gotFrame = _sharedBuffer.TryReadFrame(scanline, frameSize);
                    if (!gotFrame && retry < 2)
                        Thread.SpinWait(100);
                }

                if (gotFrame)
                    _hasLiveFrame = true;
            }

            if (!gotFrame)
            {
                if (!_hasLiveFrame)
                    FillNV12Black(scanline, width, height);
                // If _hasLiveFrame but torn read: scanline still has data from last
                // allocator cycle — Frame Server reuses sample buffers, so the previous
                // frame's data is often still there. Acceptable minor glitch vs 3MB copy.
            }
        });

        buffer.Object.SetCurrentLength((uint)frameSize).ThrowOnError();
        _frameCount++;
        return sample;
    }

    /// <summary>
    /// Fallback for RGB32 requests — just fill green (NV12→RGB conversion not implemented).
    /// </summary>
    private IComObject<IMFSample> FillFallback(IComObject<IMFSample> sample, Guid format, uint width, uint height, int frameSize)
    {
        using var buffer = sample.GetBufferByIndex(0);
        buffer.WithLock((scanline, length, _) =>
        {
            if (format == MFConstants.MFVideoFormat_NV12)
                FillNV12Black(scanline, width, height);
            else
                FillRGB32Green(scanline, width, height);
        });
        buffer.Object.SetCurrentLength((uint)frameSize).ThrowOnError();
        _frameCount++;
        return sample;
    }

    /// <summary>NV12 black: Y=16 (limited range black), UV=128 (neutral chroma).</summary>
    private static unsafe void FillNV12Black(nint scanline, uint width, uint height)
    {
        byte* ptr = (byte*)scanline;
        uint ySize = width * height;
        uint uvSize = ySize / 2;
        new Span<byte>(ptr, (int)ySize).Fill(16);         // Y plane
        new Span<byte>(ptr + ySize, (int)uvSize).Fill(128); // UV plane
    }

    private static unsafe void FillRGB32Green(nint scanline, uint width, uint height)
    {
        uint pixel = 0xFF00FF00; // BGRA: green
        uint totalPixels = width * height;
        uint* ptr = (uint*)scanline;
        for (uint i = 0; i < totalPixels; i++)
            ptr[i] = pixel;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _sharedBuffer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
