using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// Managed COM interface for IMFMediaEventGenerator (GUID 2CD0BD52-BCD5-4B89-B62C-EADC0C031E7D).
/// Defined WITHOUT [ComImport] so that C# classes (VirtualCameraSource, VirtualCameraStream)
/// can implement it and be exposed via a CCW with the correct IID.
/// </summary>
[Guid("2CD0BD52-BCD5-4B89-B62C-EADC0C031E7D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaEventGenerator
{
    [PreserveSig] int GetEvent(int dwFlags, [MarshalAs(UnmanagedType.Interface)] out IMFMediaEvent ppEvent);
    [PreserveSig] int BeginGetEvent([MarshalAs(UnmanagedType.Interface)] IMFAsyncCallback pCallback, [MarshalAs(UnmanagedType.Interface)] object punkState);
    [PreserveSig] int EndGetEvent([MarshalAs(UnmanagedType.Interface)] IMFAsyncResult pResult, [MarshalAs(UnmanagedType.Interface)] out IMFMediaEvent ppEvent);
    [PreserveSig] int QueueEvent([MarshalAs(UnmanagedType.Interface)] IMFMediaEvent pEvent);
}
