using System.Runtime.InteropServices;
using vCamService.Core.Services;
using vCamService.VCam.Interop;
using static vCamService.VCam.Interop.MFGuids;
using static vCamService.VCam.Interop.MFInterop;

namespace vCamService.VCam;

/// <summary>
/// COM-visible implementation of IMFMediaSource.
///
/// Windows Media Foundation activates this class via CoCreateInstance using the CLSID
/// registered by VirtualCameraManager.  MF calls IMFMediaSource methods on the resulting
/// COM Callable Wrapper (CCW).
///
/// Frame data is supplied out-of-band through <see cref="SharedFrameBuffer"/>, which is
/// set by <see cref="VirtualCameraManager"/> before it calls <c>virtualCamera.Start()</c>.
/// </summary>
[ComVisible(true)]
[Guid("B5823E0D-4D72-4B3F-A9B8-C12F5E7D9A3E")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class VirtualCameraSource : IMFMediaSource
{
    // ------------------------------------------------------------------
    // Shared state set by VirtualCameraManager
    // ------------------------------------------------------------------

    /// <summary>
    /// Frame data shared between the managed host and the COM source.
    /// VirtualCameraManager writes here; VirtualCameraStream reads here.
    /// </summary>
    public static FrameBuffer? SharedFrameBuffer { get; set; }

    // ------------------------------------------------------------------
    // Instance state
    // ------------------------------------------------------------------

    private readonly MediaEventQueue _eventQueue;
    private readonly VirtualCameraStream _stream;
    private readonly object _stateLock = new();
    private bool _isShutdown;

    public VirtualCameraSource()
    {
        _eventQueue = new MediaEventQueue();
        _stream = new VirtualCameraStream(this);
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
    // IMFMediaSource
    // ------------------------------------------------------------------

    public int GetCharacteristics(out int pdwCharacteristics)
    {
        pdwCharacteristics = MFMEDIASOURCE_IS_LIVE;
        return S_OK;
    }

    public int CreatePresentationDescriptor(
        [MarshalAs(UnmanagedType.Interface)] out IMFPresentationDescriptor ppPresentationDescriptor)
    {
        lock (_stateLock)
        {
            if (_isShutdown) { ppPresentationDescriptor = null!; return MF_E_SHUTDOWN; }
        }

        return BuildPresentationDescriptor(out ppPresentationDescriptor);
    }

    public int Start(
        [MarshalAs(UnmanagedType.Interface)] IMFPresentationDescriptor pPresentationDescriptor,
        ref Guid pguidTimeFormat,
        IntPtr pvarStartPosition)
    {
        lock (_stateLock)
        {
            if (_isShutdown) return MF_E_SHUTDOWN;
        }

        // Announce the stream then signal the source has started.
        _eventQueue.QueueEventParamUnk(MENewStream, _stream);
        _eventQueue.QueueEventParamVar(MESourceStarted);
        return S_OK;
    }

    public int Stop()
    {
        lock (_stateLock)
        {
            if (_isShutdown) return MF_E_SHUTDOWN;
        }

        _eventQueue.QueueEventParamVar(MESourceStopped);
        return S_OK;
    }

    public int Pause()
    {
        // Live sources do not support pausing.
        return MF_E_INVALID_STATE_TRANSITION;
    }

    public int Shutdown()
    {
        lock (_stateLock)
        {
            if (_isShutdown) return MF_E_SHUTDOWN;
            _isShutdown = true;
        }

        _stream.Shutdown();
        _eventQueue.Shutdown();
        return S_OK;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static int BuildPresentationDescriptor(out IMFPresentationDescriptor ppPD)
    {
        ppPD = null!;

        int hr = MFCreateMediaType(out IMFMediaType mediaType);
        if (hr < 0) return hr;

        // Set major type = Video
        var key = MF_MT_MAJOR_TYPE;
        var val = MFMediaType_Video;
        hr = mediaType.SetGUID(ref key, ref val);
        if (hr < 0) return hr;

        // Set sub-type = ARGB32 (BGRA byte order)
        key = MF_MT_SUBTYPE;
        val = MFVideoFormat_ARGB32;
        hr = mediaType.SetGUID(ref key, ref val);
        if (hr < 0) return hr;

        // Frame size: 1280 × 720 packed as (width << 32 | height)
        key = MF_MT_FRAME_SIZE;
        hr = mediaType.SetUINT64(ref key, PackedUInt64(1280, 720));
        if (hr < 0) return hr;

        // Frame rate: 30/1 packed as (numerator << 32 | denominator)
        key = MF_MT_FRAME_RATE;
        hr = mediaType.SetUINT64(ref key, PackedUInt64(30, 1));
        if (hr < 0) return hr;

        // Pixel aspect ratio: 1:1
        key = MF_MT_PIXEL_ASPECT_RATIO;
        hr = mediaType.SetUINT64(ref key, PackedUInt64(1, 1));
        if (hr < 0) return hr;

        // Progressive scan
        key = MF_MT_INTERLACE_MODE;
        hr = mediaType.SetUINT32(ref key, 2); // MFVideoInterlace_Progressive
        if (hr < 0) return hr;

        // Each sample is independent (no inter-frame dependencies)
        key = MF_MT_ALL_SAMPLES_INDEPENDENT;
        hr = mediaType.SetUINT32(ref key, 1);
        if (hr < 0) return hr;

        // Stream descriptor wrapping the single media type
        IMFMediaType[] types = [mediaType];
        hr = MFCreateStreamDescriptor(0, 1, types, out IMFStreamDescriptor sd);
        if (hr < 0) return hr;

        // Presentation descriptor wrapping the single stream
        IMFStreamDescriptor[] sds = [sd];
        hr = MFCreatePresentationDescriptor(1, sds, out IMFPresentationDescriptor pd);
        if (hr < 0) return hr;

        // Select stream 0 by default
        hr = pd.SelectStream(0);
        if (hr < 0) return hr;

        ppPD = pd;
        return S_OK;
    }

    private static long PackedUInt64(uint hi, uint lo) => ((long)hi << 32) | lo;
}
