using System.Runtime.InteropServices;
using vCamService.VCam.Interop;
using static vCamService.VCam.Interop.MFGuids;
using static vCamService.VCam.Interop.MFInterop;

namespace vCamService.VCam;

/// <summary>
/// Thread-safe helper that wraps a native IMFMediaEventQueue.
/// Sources delegate all IMFMediaEventGenerator calls through here.
/// </summary>
internal sealed class MediaEventQueue : IDisposable
{
    private IMFMediaEventQueue? _queue;
    private bool _disposed;

    internal MediaEventQueue()
    {
        int hr = MFCreateEventQueue(out IMFMediaEventQueue q);
        ThrowIfFailed(hr, nameof(MFCreateEventQueue));
        _queue = q;
    }

    internal int GetEvent(int dwFlags, out IMFMediaEvent ppEvent)
    {
        EnsureNotDisposed();
        return _queue!.GetEvent(dwFlags, out ppEvent);
    }

    internal int BeginGetEvent(IMFAsyncCallback pCallback, object punkState)
    {
        EnsureNotDisposed();
        return _queue!.BeginGetEvent(pCallback, punkState);
    }

    internal int EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
    {
        EnsureNotDisposed();
        return _queue!.EndGetEvent(pResult, out ppEvent);
    }

    internal int QueueEvent(IMFMediaEvent pEvent)
    {
        EnsureNotDisposed();
        return _queue!.QueueEvent(pEvent);
    }

    internal int QueueEventParamVar(int met, int hrStatus = S_OK)
    {
        EnsureNotDisposed();
        var extGuid = Guid.Empty;
        return _queue!.QueueEventParamVar(met, ref extGuid, hrStatus, IntPtr.Zero);
    }

    internal int QueueEventParamUnk(int met, object pUnk, int hrStatus = S_OK)
    {
        EnsureNotDisposed();
        var extGuid = Guid.Empty;
        return _queue!.QueueEventParamUnk(met, ref extGuid, hrStatus, pUnk);
    }

    internal void Shutdown()
    {
        _queue?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue?.Shutdown();
        _queue = null;
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void ThrowIfFailed(int hr, string context)
    {
        if (hr < 0) throw new COMException($"Media Foundation call failed in {context}", hr);
    }
}
