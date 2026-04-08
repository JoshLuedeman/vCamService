using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFAsyncResult (GUID AC6B7889-0740-4D51-8619-905994A55CC6).
/// Carries the result of an asynchronous MF operation.
/// </summary>
[ComImport]
[Guid("AC6B7889-0740-4D51-8619-905994A55CC6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFAsyncResult
{
    [PreserveSig] int GetState([MarshalAs(UnmanagedType.Interface)] out object ppunkState);
    [PreserveSig] int GetStatus();
    [PreserveSig] int SetStatus(int hrStatus);
    [PreserveSig] int GetObject([MarshalAs(UnmanagedType.Interface)] out object ppObject);
    // Returns IUnknown* without AddRef — do not release.
    [PreserveSig] IntPtr GetStateNoAddRef();
}
