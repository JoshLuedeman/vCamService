using System.Runtime.InteropServices;
using DirectN;
using vCamService.Core.Services;
using vCamService.VCam.Utilities;

namespace vCamService.VCam;

/// <summary>
/// Virtual camera media stream. Implements IMFMediaStream2 for Frame Server.
/// Uses Frame Server's provided allocator (IMFVideoSampleAllocatorEx) for samples —
/// this is the critical difference from our old code which used MFCreateSample().
/// </summary>
public class MediaStream : MFAttributes, IMFMediaStream2, IKsControl
{
    public const int NUM_ALLOCATOR_SAMPLES = 30;

    // Stream parameters — read from shared memory or fallback defaults
    public uint ImageWidth { get; }
    public uint ImageHeight { get; }
    public uint FpsNumerator { get; }
    public uint FpsDenominator { get; }

    private readonly object _lock = new();
    private readonly MediaSource _source;
    private IComObject<IMFMediaEventQueue>? _queue;
    private IComObject<IMFStreamDescriptor>? _descriptor;
    private IComObject<IMFVideoSampleAllocatorEx>? _allocator;
    private _MF_STREAM_STATE _state;
    private Guid _format;
    private FrameGenerator _generator = new();

    public MediaStream(MediaSource source, uint index)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(source);
            _source = source;

            // Read stream dimensions from config file (written by app after probing)
            StreamConfig? config = null;
            try { config = StreamConfig.Load(); } catch { }

            if (config != null && config.Width > 0 && config.Height > 0)
            {
                ImageWidth = (uint)config.Width;
                ImageHeight = (uint)config.Height;
                FpsNumerator = (uint)config.FpsNumerator;
                FpsDenominator = (uint)Math.Max(config.FpsDenominator, 1);
                EventProvider.LogInfo($"StreamConfig: {ImageWidth}x{ImageHeight} @ {FpsNumerator}/{FpsDenominator}fps");
            }
            else
            {
                ImageWidth = 1280;
                ImageHeight = 720;
                FpsNumerator = 30;
                FpsDenominator = 1;
                EventProvider.LogInfo("No stream config, using defaults 1280x720@30fps");
            }

            // Create the shared memory buffer NOW (at source activation, not lazily).
            // This lets the app detect it immediately and start writing frames faster.
            _generator.CreateBuffer(config);
            EventProvider.LogInfo("Shared memory buffer created at source activation");

            SetGUID(MFConstants.MF_DEVICESTREAM_STREAM_CATEGORY, KSMedia.PINNAME_VIDEO_CAPTURE).ThrowOnError();
            SetUINT32(MFConstants.MF_DEVICESTREAM_STREAM_ID, index).ThrowOnError();
            SetUINT32(MFConstants.MF_DEVICESTREAM_FRAMESERVER_SHARED, 1).ThrowOnError();
            SetUINT32(MFConstants.MF_DEVICESTREAM_ATTRIBUTE_FRAMESOURCE_TYPES, (uint)_MFFrameSourceTypes.MFFrameSourceTypes_Color).ThrowOnError();

            Functions.MFCreateEventQueue(out var queue).ThrowOnError();
            _queue = new ComObject<IMFMediaEventQueue>(queue);

            // Offer both NV12 (preferred, zero-copy) and RGB32 (fallback)
            var mediaTypes = new IMFMediaType[2];

            // NV12 first — preferred by Discord, Teams, and most video apps
            Functions.MFCreateMediaType(out var nv12Type).ThrowOnError();
            nv12Type.SetGUID(MFConstants.MF_MT_MAJOR_TYPE, MFConstants.MFMediaType_Video).ThrowOnError();
            nv12Type.SetGUID(MFConstants.MF_MT_SUBTYPE, MFConstants.MFVideoFormat_NV12).ThrowOnError();
            nv12Type.SetSize(MFConstants.MF_MT_FRAME_SIZE, ImageWidth, ImageHeight);
            nv12Type.SetUINT32(MFConstants.MF_MT_DEFAULT_STRIDE, ImageWidth * 3 / 2).ThrowOnError();
            nv12Type.SetUINT32(MFConstants.MF_MT_INTERLACE_MODE, (uint)_MFVideoInterlaceMode.MFVideoInterlace_Progressive).ThrowOnError();
            nv12Type.SetUINT32(MFConstants.MF_MT_ALL_SAMPLES_INDEPENDENT, 1).ThrowOnError();
            nv12Type.SetRatio(MFConstants.MF_MT_FRAME_RATE, FpsNumerator, FpsDenominator);
            var nv12bitrate = ImageWidth * 3 * ImageHeight * 8 * FpsNumerator / (FpsDenominator * 2);
            nv12Type.SetUINT32(MFConstants.MF_MT_AVG_BITRATE, (uint)nv12bitrate).ThrowOnError();
            nv12Type.SetRatio(MFConstants.MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            mediaTypes[0] = nv12Type;

            // RGB32 fallback
            Functions.MFCreateMediaType(out var rgbType).ThrowOnError();
            rgbType.SetGUID(MFConstants.MF_MT_MAJOR_TYPE, MFConstants.MFMediaType_Video).ThrowOnError();
            rgbType.SetGUID(MFConstants.MF_MT_SUBTYPE, MFConstants.MFVideoFormat_RGB32).ThrowOnError();
            rgbType.SetSize(MFConstants.MF_MT_FRAME_SIZE, ImageWidth, ImageHeight);
            rgbType.SetUINT32(MFConstants.MF_MT_DEFAULT_STRIDE, ImageWidth * 4).ThrowOnError();
            rgbType.SetUINT32(MFConstants.MF_MT_INTERLACE_MODE, (uint)_MFVideoInterlaceMode.MFVideoInterlace_Progressive).ThrowOnError();
            rgbType.SetUINT32(MFConstants.MF_MT_ALL_SAMPLES_INDEPENDENT, 1).ThrowOnError();
            rgbType.SetRatio(MFConstants.MF_MT_FRAME_RATE, FpsNumerator, FpsDenominator);
            var bitrate = ImageWidth * 4 * ImageHeight * 8 * FpsNumerator / FpsDenominator;
            rgbType.SetUINT32(MFConstants.MF_MT_AVG_BITRATE, (uint)bitrate).ThrowOnError();
            rgbType.SetRatio(MFConstants.MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            mediaTypes[1] = rgbType;

            Functions.MFCreateStreamDescriptor(index, mediaTypes.Length, mediaTypes, out var descriptor).ThrowOnError();
            descriptor.GetMediaTypeHandler(out var handler).ThrowOnError();
            handler.SetCurrentMediaType(mediaTypes[0]).ThrowOnError();
            _descriptor = new ComObject<IMFStreamDescriptor>(descriptor);
        }
        catch (Exception e)
        {
            EventProvider.LogError(e.ToString());
            throw;
        }
    }

    public HRESULT Start(IMFMediaType? type)
    {
        var queue = _queue;
        var allocator = _allocator;
        if (queue == null || allocator == null)
        {
            EventProvider.LogInfo("MF_E_SHUTDOWN");
            return HRESULTS.MF_E_SHUTDOWN;
        }

        if (type != null)
        {
            allocator.Object.InitializeSampleAllocator(NUM_ALLOCATOR_SAMPLES, type).ThrowOnError();
            type.GetGUID(MFConstants.MF_MT_SUBTYPE, out _format).ThrowOnError();
            EventProvider.LogInfo("Format: " + _format);
        }

        queue.Object.QueueEventParamVar(
            (uint)__MIDL___MIDL_itf_mfobjects_0000_0013_0001.MEStreamStarted,
            Guid.Empty, HRESULTS.S_OK, null).ThrowOnError();
        _state = _MF_STREAM_STATE.MF_STREAM_STATE_RUNNING;
        EventProvider.LogInfo("Started");
        return HRESULTS.S_OK;
    }

    public HRESULT Stop()
    {
        var queue = _queue;
        var allocator = _allocator;
        if (queue == null || allocator == null)
        {
            EventProvider.LogInfo("MF_E_SHUTDOWN");
            return HRESULTS.MF_E_SHUTDOWN;
        }

        allocator.Object.UninitializeSampleAllocator();
        queue.Object.QueueEventParamVar(
            (uint)__MIDL___MIDL_itf_mfobjects_0000_0013_0001.MEStreamStopped,
            Guid.Empty, HRESULTS.S_OK, null).ThrowOnError();
        _state = _MF_STREAM_STATE.MF_STREAM_STATE_STOPPED;
        return HRESULTS.S_OK;
    }

    public static MFSampleAllocatorUsage GetAllocatorUsage()
        => MFSampleAllocatorUsage.MFSampleAllocatorUsage_UsesProvidedAllocator;

    public HRESULT SetAllocator(object allocator)
    {
        if (allocator == null)
        {
            EventProvider.LogInfo("E_POINTER");
            return HRESULTS.E_POINTER;
        }

        if (allocator is not IMFVideoSampleAllocatorEx aex)
        {
            EventProvider.LogInfo("E_NOINTERFACE");
            return HRESULTS.E_NOINTERFACE;
        }

        _allocator = new ComObject<IMFVideoSampleAllocatorEx>(aex);
        return HRESULTS.S_OK;
    }

    public HRESULT Set3DManager(object manager)
    {
        var allocator = _allocator;
        if (allocator == null)
        {
            EventProvider.LogInfo("E_POINTER");
            return HRESULTS.E_POINTER;
        }

        allocator.Object.SetDirectXManager(manager).ThrowOnError();
        _generator.SetD3DManager(manager, ImageWidth, ImageHeight);
        return HRESULTS.S_OK;
    }

    // IMFMediaEventGenerator
    public HRESULT GetEvent(uint flags, out IMFMediaEvent evt)
    {
        try
        {
            lock (_lock)
            {
                var queue = _queue;
                if (queue == null) { evt = null!; return HRESULTS.MF_E_SHUTDOWN; }
                return queue.Object.GetEvent(flags, out evt);
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT BeginGetEvent(IMFAsyncCallback callback, object state)
    {
        try
        {
            lock (_lock)
            {
                var queue = _queue;
                if (queue == null) return HRESULTS.MF_E_SHUTDOWN;
                return queue.Object.BeginGetEvent(callback, state);
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT EndGetEvent(IMFAsyncResult result, out IMFMediaEvent evt)
    {
        try
        {
            lock (_lock)
            {
                var queue = _queue;
                if (queue == null) { evt = null!; return HRESULTS.MF_E_SHUTDOWN; }
                return queue.Object.EndGetEvent(result, out evt);
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT QueueEvent(uint type, Guid extendedType, HRESULT hrStatus, PROPVARIANT value)
    {
        try
        {
            lock (_lock)
            {
                var queue = _queue;
                if (queue == null) return HRESULTS.MF_E_SHUTDOWN;
                return queue.Object.QueueEventParamVar(type, extendedType, hrStatus, value);
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    // IMFMediaStream
    public HRESULT GetMediaSource(out IMFMediaSource mediaSource)
    {
        lock (_lock)
        {
            mediaSource = _source;
            return HRESULTS.S_OK;
        }
    }

    public HRESULT GetStreamDescriptor(out IMFStreamDescriptor streamDescriptor)
    {
        try
        {
            lock (_lock)
            {
                var descriptor = _descriptor;
                if (descriptor == null) { streamDescriptor = null!; return HRESULTS.MF_E_SHUTDOWN; }
                streamDescriptor = descriptor.Object;
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT RequestSample(object token)
    {
        try
        {
            IMFMediaEventQueue? queue;
            IMFVideoSampleAllocatorEx? allocator;
            lock (_lock)
            {
                if (_queue == null || _allocator == null)
                    return HRESULTS.MF_E_SHUTDOWN;
                queue = _queue.Object;
                allocator = _allocator.Object;
            }

            // Allocate from Frame Server's allocator.
            // ComObject.Dispose() calls Marshal.ReleaseComObject deterministically,
            // so the RCW ref is released at end of this method. Frame Server holds
            // its own AddRef'd reference — sample returns to pool when FS finishes.
            // No periodic GC.Collect needed.
            allocator.AllocateSample(out var sample).ThrowOnError();
            using var sampleWrapper = new ComObject<IMFSample>(sample);

            sample.SetSampleTime(Functions.MFGetSystemTime()).ThrowOnError();
            long duration = FpsDenominator > 0 ? (10_000_000L * FpsDenominator / FpsNumerator) : 333333;
            sample.SetSampleDuration(duration).ThrowOnError();

            _generator.Generate(sampleWrapper, _format, ImageWidth, ImageHeight);

            if (token != null)
            {
                sample.SetUnknown(MFConstants.MFSampleExtension_Token, token).ThrowOnError();
            }

            queue.QueueEventParamUnk(
                (uint)__MIDL___MIDL_itf_mfobjects_0000_0013_0001.MEMediaSample,
                Guid.Empty, HRESULTS.S_OK, sample).ThrowOnError();

            // Release RCW refs so samples return to allocator pool
            if (_generator.FrameCount % (NUM_ALLOCATOR_SAMPLES / 2) == 0)
            {
                GC.Collect();
            }

            return HRESULTS.S_OK;
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    // IMFMediaStream2
    public HRESULT SetStreamState(_MF_STREAM_STATE value)
    {
        EventProvider.LogInfo($"value:{value}");
        try
        {
            if (_state != value)
            {
                switch (value)
                {
                    case _MF_STREAM_STATE.MF_STREAM_STATE_STOPPED:
                        return Stop();
                    case _MF_STREAM_STATE.MF_STREAM_STATE_PAUSED:
                        if (_state != _MF_STREAM_STATE.MF_STREAM_STATE_RUNNING)
                            return HRESULTS.MF_E_INVALID_STATE_TRANSITION;
                        _state = value;
                        break;
                    case _MF_STREAM_STATE.MF_STREAM_STATE_RUNNING:
                        return Start(null);
                    default:
                        return HRESULTS.MF_E_INVALID_STATE_TRANSITION;
                }
            }
            return HRESULTS.S_OK;
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT GetStreamState(out _MF_STREAM_STATE value)
    {
        value = _state;
        return HRESULTS.S_OK;
    }

    // IKsControl
    public HRESULT KsProperty(ref KSIDENTIFIER property, uint propertyLength, nint propertyData, uint dataLength, out uint bytesReturned)
    {
        bytesReturned = 0;
        return HRESULT.FromWin32(VCamConstants.ERROR_SET_NOT_FOUND);
    }

    public HRESULT KsMethod(ref KSIDENTIFIER method, uint methodLength, nint methodData, uint dataLength, out uint bytesReturned)
    {
        bytesReturned = 0;
        return HRESULT.FromWin32(VCamConstants.ERROR_SET_NOT_FOUND);
    }

    public HRESULT KsEvent(ref KSIDENTIFIER evt, uint eventLength, nint eventData, uint dataLength, out uint bytesReturned)
    {
        bytesReturned = 0;
        return HRESULT.FromWin32(VCamConstants.ERROR_SET_NOT_FOUND);
    }

    protected override void Dispose(bool disposing)
    {
        EventProvider.LogInfo();
        Interlocked.Exchange(ref _descriptor!, null)?.Dispose();
        Interlocked.Exchange(ref _queue!, null)?.Dispose();
        Interlocked.Exchange(ref _allocator!, null)?.Dispose();
        Interlocked.Exchange(ref _generator!, null)?.Dispose();
        base.Dispose(disposing);
    }
}
