using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// Managed COM interface for IMFMediaStream (GUID D182108F-4EC6-443F-AA42-A71106EC825F).
/// Defined WITHOUT [ComImport] so VirtualCameraStream can implement it and expose a CCW
/// with the correct IID.
///
/// Vtable layout (after IUnknown):
///   [0] GetEvent        \
///   [1] BeginGetEvent    |  IMFMediaEventGenerator
///   [2] EndGetEvent      |
///   [3] QueueEvent      /
///   [4] GetMediaSource       \
///   [5] GetStreamDescriptor   |  IMFMediaStream
///   [6] RequestSample        /
/// </summary>
[Guid("D182108F-4EC6-443F-AA42-A71106EC825F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaStream : IMFMediaEventGenerator
{
    [PreserveSig] int GetMediaSource([MarshalAs(UnmanagedType.Interface)] out IMFMediaSource ppMediaSource);
    [PreserveSig] int GetStreamDescriptor([MarshalAs(UnmanagedType.Interface)] out IMFStreamDescriptor ppStreamDescriptor);
    [PreserveSig] int RequestSample([MarshalAs(UnmanagedType.Interface)] object pToken);
}
