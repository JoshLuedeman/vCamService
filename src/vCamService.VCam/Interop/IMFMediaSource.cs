using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// Managed COM interface for IMFMediaSource (GUID 279A808D-AEC7-40C8-9C6B-A6B492C78A66).
/// Defined WITHOUT [ComImport] so VirtualCameraSource can implement it and expose a CCW
/// with the correct IID for Windows Media Foundation to call.
///
/// Vtable layout (after IUnknown) when implemented by a C# class:
///   [0] GetEvent          \
///   [1] BeginGetEvent      |  IMFMediaEventGenerator
///   [2] EndGetEvent        |
///   [3] QueueEvent        /
///   [4] GetCharacteristics         \
///   [5] CreatePresentationDescriptor|  IMFMediaSource
///   [6] Start                       |
///   [7] Stop                        |
///   [8] Pause                       |
///   [9] Shutdown                   /
/// </summary>
[Guid("279A808D-AEC7-40C8-9C6B-A6B492C78A66")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaSource : IMFMediaEventGenerator
{
    [PreserveSig] int GetCharacteristics(out int pdwCharacteristics);
    [PreserveSig] int CreatePresentationDescriptor([MarshalAs(UnmanagedType.Interface)] out IMFPresentationDescriptor ppPresentationDescriptor);

    /// <param name="pPresentationDescriptor">Presentation descriptor from CreatePresentationDescriptor.</param>
    /// <param name="pguidTimeFormat">Time format GUID; GUID_NULL means 100-ns units.</param>
    /// <param name="pvarStartPosition">PROPVARIANT* — start position; IntPtr lets us accept null/VT_EMPTY.</param>
    [PreserveSig] int Start([MarshalAs(UnmanagedType.Interface)] IMFPresentationDescriptor pPresentationDescriptor, ref Guid pguidTimeFormat, IntPtr pvarStartPosition);

    [PreserveSig] int Stop();
    [PreserveSig] int Pause();
    [PreserveSig] int Shutdown();
}
