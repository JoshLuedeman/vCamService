using System.Runtime.InteropServices;
using vCamService.VCam.Interop;
using static vCamService.VCam.Interop.MFGuids;
using static vCamService.VCam.Interop.MFInterop;

namespace vCamService.VCam;

/// <summary>
/// COM-visible implementation of IMFMediaStream.
///
/// Delivers BGRA video frames as IMFSample objects each time Windows Media Foundation
/// calls <see cref="RequestSample"/>.  Frame data is read from
/// <see cref="VirtualCameraSource.SharedFrameBuffer"/>; when no frame is available a
/// dark-gray 1280×720 slate is produced instead.
/// </summary>
[ComVisible(true)]
[Guid("C6934F1E-5E83-4C40-B0C9-D2306F8EAB4F")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class VirtualCameraStream : IMFMediaStream
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 720;
    private const int BytesPerPixel = 4; // BGRA
    private const long FrameDuration100Ns = 333_333; // 1/30 s in 100-ns units

    private readonly VirtualCameraSource _source;
    private readonly MediaEventQueue _eventQueue;
    private readonly IMFStreamDescriptor _streamDescriptor;

    private readonly object _stateLock = new();
    private bool _isShutdown;

    internal VirtualCameraStream(VirtualCameraSource source)
    {
        _source = source;
        _eventQueue = new MediaEventQueue();
        _streamDescriptor = BuildStreamDescriptor();
    }

    // ------------------------------------------------------------------
    // IMFMediaEventGenerator — delegated to the native event queue
    // ------------------------------------------------------------------

    public int GetEvent(int dwFlags, [MarshalAs(UnmanagedType.Interface)] out IMFMediaEvent ppEvent)
    {
        lock (_stateLock)
        {
            if (_isShutdown) { ppEvent = null!; return MF_E_SHUTDOWN; }
        }
        return _eventQueue.GetEvent(dwFlags, out ppEvent);
    }

    public int BeginGetEvent(
        [MarshalAs(UnmanagedType.Interface)] IMFAsyncCallback pCallback,
        [MarshalAs(UnmanagedType.Interface)] object punkState)
    {
        lock (_stateLock)
        {
            if (_isShutdown) return MF_E_SHUTDOWN;
        }
        return _eventQueue.BeginGetEvent(pCallback, punkState);
    }

    public int EndGetEvent(
        [MarshalAs(UnmanagedType.Interface)] IMFAsyncResult pResult,
        [MarshalAs(UnmanagedType.Interface)] out IMFMediaEvent ppEvent)
    {
        lock (_stateLock)
        {
            if (_isShutdown) { ppEvent = null!; return MF_E_SHUTDOWN; }
        }
        return _eventQueue.EndGetEvent(pResult, out ppEvent);
    }

    public int QueueEvent([MarshalAs(UnmanagedType.Interface)] IMFMediaEvent pEvent)
    {
        lock (_stateLock)
        {
            if (_isShutdown) return MF_E_SHUTDOWN;
        }
        return _eventQueue.QueueEvent(pEvent);
    }

    // ------------------------------------------------------------------
    // IMFMediaStream
    // ------------------------------------------------------------------

    public int GetMediaSource([MarshalAs(UnmanagedType.Interface)] out IMFMediaSource ppMediaSource)
    {
        ppMediaSource = _source;
        return S_OK;
    }

    public int GetStreamDescriptor([MarshalAs(UnmanagedType.Interface)] out IMFStreamDescriptor ppStreamDescriptor)
    {
        lock (_stateLock)
        {
            if (_isShutdown) { ppStreamDescriptor = null!; return MF_E_SHUTDOWN; }
        }
        ppStreamDescriptor = _streamDescriptor;
        return S_OK;
    }

    /// <summary>
    /// Called by MF to request the next video frame.  Creates an IMFSample containing the
    /// latest BGRA frame (or a slate) and fires a MEMediaSample event.
    /// </summary>
    public int RequestSample([MarshalAs(UnmanagedType.Interface)] object pToken)
    {
        lock (_stateLock)
        {
            if (_isShutdown) return MF_E_SHUTDOWN;
        }

        try
        {
            byte[] bgraData = GetOrCreateFrame(out int frameWidth, out int frameHeight);
            int frameByteCount = frameWidth * frameHeight * BytesPerPixel;

            int hr = MFCreateSample(out IMFSample sample);
            if (hr < 0) return hr;

            hr = MFCreateMemoryBuffer(frameByteCount, out IMFMediaBuffer buffer);
            if (hr < 0) return hr;

            // Copy pixel data into the native buffer
            hr = buffer.Lock(out IntPtr pData, out _, out _);
            if (hr < 0) return hr;

            try
            {
                int copyBytes = Math.Min(bgraData.Length, frameByteCount);
                Marshal.Copy(bgraData, 0, pData, copyBytes);
            }
            finally
            {
                buffer.Unlock();
            }

            buffer.SetCurrentLength(Math.Min(bgraData.Length, frameByteCount));

            hr = sample.AddBuffer(buffer);
            if (hr < 0) return hr;

            // Timestamp in 100-nanosecond units from process start
            long sampleTime = Environment.TickCount64 * 10_000L;
            sample.SetSampleTime(sampleTime);
            sample.SetSampleDuration(FrameDuration100Ns);

            // Deliver the sample via the event queue
            return _eventQueue.QueueEventParamUnk(MEMediaSample, sample);
        }
        catch (Exception ex)
        {
            return ex.HResult != 0 ? ex.HResult : E_FAIL;
        }
    }

    // ------------------------------------------------------------------
    // Internal lifecycle
    // ------------------------------------------------------------------

    internal void Shutdown()
    {
        lock (_stateLock)
        {
            if (_isShutdown) return;
            _isShutdown = true;
        }
        _eventQueue.Shutdown();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static byte[] GetOrCreateFrame(out int width, out int height)
    {
        var (data, w, h) = VirtualCameraSource.SharedFrameBuffer?.Get() ?? (null, 0, 0);

        if (data != null && data.Length > 0 && w > 0 && h > 0)
        {
            width = w;
            height = h;
            return data;
        }

        // No live frame yet — return a dark-gray slate at the default resolution.
        width = DefaultWidth;
        height = DefaultHeight;
        return CreateSlateFrame(DefaultWidth, DefaultHeight);
    }

    /// <summary>Creates a solid dark-gray BGRA frame (B=64, G=64, R=64, A=255).</summary>
    private static byte[] CreateSlateFrame(int width, int height)
    {
        int pixelCount = width * height;
        var data = new byte[pixelCount * BytesPerPixel];

        for (int i = 0; i < data.Length; i += BytesPerPixel)
        {
            data[i]     = 64;  // B
            data[i + 1] = 64;  // G
            data[i + 2] = 64;  // R
            data[i + 3] = 255; // A
        }

        return data;
    }

    /// <summary>Builds a stream descriptor advertising BGRA 1280×720 @ 30 fps.</summary>
    private static IMFStreamDescriptor BuildStreamDescriptor()
    {
        int hr = MFCreateMediaType(out IMFMediaType mediaType);
        ThrowIfFailed(hr, nameof(MFCreateMediaType));

        var key = MF_MT_MAJOR_TYPE;
        var val = MFMediaType_Video;
        mediaType.SetGUID(ref key, ref val);

        key = MF_MT_SUBTYPE;
        val = MFVideoFormat_ARGB32;
        mediaType.SetGUID(ref key, ref val);

        key = MF_MT_FRAME_SIZE;
        mediaType.SetUINT64(ref key, PackedUInt64(DefaultWidth, DefaultHeight));

        key = MF_MT_FRAME_RATE;
        mediaType.SetUINT64(ref key, PackedUInt64(30, 1));

        key = MF_MT_PIXEL_ASPECT_RATIO;
        mediaType.SetUINT64(ref key, PackedUInt64(1, 1));

        key = MF_MT_INTERLACE_MODE;
        mediaType.SetUINT32(ref key, 2); // Progressive

        key = MF_MT_ALL_SAMPLES_INDEPENDENT;
        mediaType.SetUINT32(ref key, 1);

        IMFMediaType[] types = [mediaType];
        hr = MFCreateStreamDescriptor(0, 1, types, out IMFStreamDescriptor sd);
        ThrowIfFailed(hr, nameof(MFCreateStreamDescriptor));

        return sd;
    }

    private static long PackedUInt64(uint hi, uint lo) => ((long)hi << 32) | lo;

    private static void ThrowIfFailed(int hr, string context)
    {
        if (hr < 0) throw new COMException($"MF call failed in {context}", hr);
    }
}
