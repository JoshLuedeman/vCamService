using System.Runtime.InteropServices;
using DirectN;
using vCamService.VCam.Utilities;

namespace vCamService.VCam;

/// <summary>
/// Virtual camera media source. Implements IMFMediaSourceEx for Frame Server.
/// Manages streams, presentation descriptor, sensor profiles, and allocator routing.
/// </summary>
public class MediaSource : MFAttributes, IMFMediaSourceEx, IMFSampleAllocatorControl, IMFGetService, IKsControl, ICustomQueryInterface
{
    private readonly object _lock = new();
    private readonly MediaStream[] _streams;
    private IComObject<IMFMediaEventQueue>? _queue;
    private IComObject<IMFPresentationDescriptor>? _presentationDescriptor;

    public MediaSource()
    {
        try
        {
            _streams = [new MediaStream(this, 0)];

            // Sensor profiles required by Frame Server
            uint streamId = 0;
            Functions.MFCreateSensorProfile(KSMedia.KSCAMERAPROFILE_Legacy, 0, null, out var legacy).ThrowOnError();
            legacy.AddProfileFilter(streamId, "((RES==;FRT<=30,1;SUT==))").ThrowOnError();

            Functions.MFCreateSensorProfile(KSMedia.KSCAMERAPROFILE_HighFrameRate, 0, null, out var high).ThrowOnError();
            high.AddProfileFilter(streamId, "((RES==;FRT>=60,1;SUT==))").ThrowOnError();

            Functions.MFCreateSensorProfileCollection(out var collection).ThrowOnError();
            collection.AddProfile(legacy).ThrowOnError();
            collection.AddProfile(high).ThrowOnError();
            SetUnknown(MFConstants.MF_DEVICEMFT_SENSORPROFILE_COLLECTION, collection).ThrowOnError();

            // Presentation descriptor
            var descriptors = new IMFStreamDescriptor[_streams.Length];
            for (var i = 0; i < descriptors.Length; i++)
            {
                _streams[i].GetStreamDescriptor(out descriptors[i]).ThrowOnError();
            }
            Functions.MFCreatePresentationDescriptor(descriptors.Length, descriptors, out var descriptor).ThrowOnError();
            _presentationDescriptor = new ComObject<IMFPresentationDescriptor>(descriptor);

            Functions.MFCreateEventQueue(out var queue).ThrowOnError();
            _queue = new ComObject<IMFMediaEventQueue>(queue);
        }
        catch (Exception e)
        {
            EventProvider.LogError(e.ToString());
            throw;
        }
    }

    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out nint ppv)
    {
        // Only log unexpected QIs to reduce noise
        if (iid != typeof(IKsControl).GUID && iid != typeof(IMFAttributes).GUID &&
            iid != typeof(IMFGetService).GUID && iid != typeof(IMFMediaSourceEx).GUID &&
            iid != VCamConstants.IID_IMFDeviceSourceInternal && iid != VCamConstants.IID_IMFDeviceSourceStatus &&
            iid != VCamConstants.IID_IMFDeviceController && iid != VCamConstants.IID_IMFDeviceController2)
        {
            EventProvider.LogInfo($"iid{iid:B}");
        }
        ppv = 0;
        return CustomQueryInterfaceResult.NotHandled;
    }

    private int GetStreamIndexById(uint id)
    {
        for (var i = 0; i < _streams.Length; i++)
        {
            if (_streams[i].GetStreamDescriptor(out var desc).IsError) return -1;
            if (desc.GetStreamIdentifier(out var sid).IsError) return -1;
            if (sid == id) return i;
        }
        return -1;
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
        lock (_lock)
        {
            var queue = _queue;
            if (queue == null) return HRESULTS.MF_E_SHUTDOWN;
            return queue.Object.QueueEventParamVar(type, extendedType, hrStatus, value);
        }
    }

    // IMFMediaSource
    public HRESULT GetCharacteristics(out uint characteristics)
    {
        characteristics = (uint)_MFMEDIASOURCE_CHARACTERISTICS.MFMEDIASOURCE_IS_LIVE;
        return HRESULTS.S_OK;
    }

    public HRESULT CreatePresentationDescriptor(out IMFPresentationDescriptor presentationDescriptor)
    {
        try
        {
            lock (_lock)
            {
                if (_presentationDescriptor == null)
                {
                    presentationDescriptor = null!;
                    return HRESULTS.MF_E_SHUTDOWN;
                }
                return _presentationDescriptor.Object.Clone(out presentationDescriptor);
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT Start(IMFPresentationDescriptor presentationDescriptor, nint guidTimeFormat, PROPVARIANT startPosition)
    {
        try
        {
            EventProvider.LogInfo($"presentationDescriptor:{presentationDescriptor} guidTimeFormat:{guidTimeFormat}");
            if (guidTimeFormat != IntPtr.Zero)
            {
                var guid = Marshal.PtrToStructure<Guid>(guidTimeFormat);
                if (guid != Guid.Empty) return HRESULTS.E_INVALIDARG;
            }

            lock (_lock)
            {
                var queue = _queue;
                var ps = _presentationDescriptor;
                if (queue == null || ps == null) return HRESULTS.MF_E_SHUTDOWN;

                presentationDescriptor.GetStreamDescriptorCount(out var count);
                EventProvider.LogInfo($"descriptors count:{count}");
                if (count != _streams.Length) return HRESULTS.E_INVALIDARG;

                for (var i = 0; i < count; i++)
                {
                    presentationDescriptor.GetStreamDescriptorByIndex((uint)i, out var selected, out var descriptor).ThrowOnError();
                    descriptor.GetStreamIdentifier(out var id).ThrowOnError();

                    var index = GetStreamIndexById(id);
                    if (index < 0) return HRESULTS.E_INVALIDARG;

                    ps.Object.GetStreamDescriptorByIndex((uint)index, out var thisSelected, out var thisDescriptor).ThrowOnError();
                    _streams[i].GetStreamState(out var state).ThrowOnError();

                    if (thisSelected && state == _MF_STREAM_STATE.MF_STREAM_STATE_STOPPED)
                        thisSelected = false;
                    else if (!thisSelected && state != _MF_STREAM_STATE.MF_STREAM_STATE_STOPPED)
                        thisSelected = true;

                    if (selected != thisSelected)
                    {
                        if (selected)
                        {
                            ps.Object.SelectStream((uint)index).ThrowOnError();
                            queue.Object.QueueEventParamUnk(
                                (uint)__MIDL___MIDL_itf_mfobjects_0000_0013_0001.MENewStream,
                                Guid.Empty, HRESULTS.S_OK, _streams[index]).ThrowOnError();
                            descriptor.GetMediaTypeHandler(out var handler).ThrowOnError();
                            handler.GetCurrentMediaType(out var type).ThrowOnError();
                            _streams[index].Start(type).ThrowOnError();
                        }
                        else
                        {
                            ps.Object.DeselectStream((uint)index).ThrowOnError();
                            _streams[index].Stop().ThrowOnError();
                        }
                    }
                }

                var time = Functions.MFGetSystemTime();
                using var pv = new PropVariant(time);
                var detached = pv.Detached;
                queue.Object.QueueEventParamVar(
                    (uint)__MIDL___MIDL_itf_mfobjects_0000_0013_0001.MESourceStarted,
                    Guid.Empty, HRESULTS.S_OK, detached).ThrowOnError();
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT Stop()
    {
        try
        {
            EventProvider.LogInfo();
            lock (_lock)
            {
                var queue = _queue;
                var pd = _presentationDescriptor;
                if (queue == null || pd == null) return HRESULTS.MF_E_SHUTDOWN;

                for (var i = 0; i < _streams.Length; i++)
                {
                    _streams[i].Stop().ThrowOnError();
                    pd.Object.DeselectStream((uint)i).ThrowOnError();
                }

                var time = Functions.MFGetSystemTime();
                using var pv = new PropVariant(time);
                var detached = pv.Detached;
                queue.Object.QueueEventParamVar(
                    (uint)__MIDL___MIDL_itf_mfobjects_0000_0013_0001.MESourceStopped,
                    Guid.Empty, HRESULTS.S_OK, detached).ThrowOnError();
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT Pause() => HRESULTS.MF_E_INVALID_STATE_TRANSITION;

    public HRESULT Shutdown()
    {
        EventProvider.LogInfo();
        try
        {
            lock (_lock)
            {
                var queue = _queue;
                if (queue == null) return HRESULTS.MF_E_SHUTDOWN;
                queue.Object.Shutdown().ThrowOnError();
                Attributes.DeleteAllItems();
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    // IMFMediaSourceEx
    public HRESULT GetSourceAttributes(out IMFAttributes attributes)
    {
        attributes = this;
        return HRESULTS.S_OK;
    }

    public HRESULT GetStreamAttributes(uint streamIdentifier, out IMFAttributes attributes)
    {
        try
        {
            lock (_lock)
            {
                if (streamIdentifier >= _streams.Length) { attributes = null!; return HRESULTS.E_FAIL; }
                var index = GetStreamIndexById(streamIdentifier);
                if (index < 0) { attributes = null!; return HRESULTS.E_FAIL; }
                attributes = _streams[index];
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT SetD3DManager(object manager)
    {
        EventProvider.LogInfo($"manager:{manager}");
        try
        {
            lock (_lock)
            {
                foreach (var stream in _streams)
                {
                    var hr = stream.Set3DManager(manager);
                    if (!hr.IsSuccess) return hr;
                }
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    // IMFSampleAllocatorControl
    public HRESULT SetDefaultAllocator(uint outputStreamID, object allocator)
    {
        EventProvider.LogInfo($"outputStreamID:{outputStreamID}");
        try
        {
            lock (_lock)
            {
                if (outputStreamID >= _streams.Length) return HRESULTS.E_FAIL;
                var index = GetStreamIndexById(outputStreamID);
                if (index < 0) return HRESULTS.E_FAIL;
                return _streams[index].SetAllocator(allocator);
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    public HRESULT GetAllocatorUsage(uint outputStreamID, out uint inputStreamID, out MFSampleAllocatorUsage usage)
    {
        try
        {
            lock (_lock)
            {
                if (outputStreamID >= _streams.Length)
                {
                    inputStreamID = 0; usage = 0; return HRESULTS.E_FAIL;
                }
                var index = GetStreamIndexById(outputStreamID);
                if (index < 0)
                {
                    inputStreamID = 0; usage = 0; return HRESULTS.E_FAIL;
                }
                inputStreamID = outputStreamID;
                usage = MediaStream.GetAllocatorUsage();
                return HRESULTS.S_OK;
            }
        }
        catch (Exception e) { EventProvider.LogError(e.ToString()); throw; }
    }

    // IMFGetService
    public HRESULT GetService(Guid guidService, Guid riid, out object ppv)
    {
        ppv = null!;
        return HRESULTS.E_NOINTERFACE;
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
        Shutdown();
        Interlocked.Exchange(ref _presentationDescriptor!, null)?.Dispose();
        Interlocked.Exchange(ref _queue!, null)?.Dispose();
        foreach (var stream in _streams) stream.Dispose();
        base.Dispose(disposing);
    }
}
