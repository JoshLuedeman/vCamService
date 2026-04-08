using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFMediaEventQueue (GUID 36F846FC-2256-48B6-B58E-E2B638316581).
/// Native helper that implements IMFMediaEventGenerator internally; our C# sources
/// delegate their Get/BeginGet/EndGet calls to this queue.
/// </summary>
[ComImport]
[Guid("36F846FC-2256-48B6-B58E-E2B638316581")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaEventQueue
{
    // Mirrors IMFMediaEventGenerator (same vtable slots)
    [PreserveSig] int GetEvent(int dwFlags, [MarshalAs(UnmanagedType.Interface)] out IMFMediaEvent ppEvent);
    [PreserveSig] int BeginGetEvent([MarshalAs(UnmanagedType.Interface)] IMFAsyncCallback pCallback, [MarshalAs(UnmanagedType.Interface)] object punkState);
    [PreserveSig] int EndGetEvent([MarshalAs(UnmanagedType.Interface)] IMFAsyncResult pResult, [MarshalAs(UnmanagedType.Interface)] out IMFMediaEvent ppEvent);
    [PreserveSig] int QueueEvent([MarshalAs(UnmanagedType.Interface)] IMFMediaEvent pEvent);

    // Queue helpers — use these to fire events without constructing an IMFMediaEvent manually
    [PreserveSig] int QueueEventParamVar(int met, ref Guid guidExtendedType, int hrStatus, IntPtr pvValue);
    [PreserveSig] int QueueEventParamUnk(int met, ref Guid guidExtendedType, int hrStatus, [MarshalAs(UnmanagedType.Interface)] object pUnk);

    [PreserveSig] int Shutdown();
}
