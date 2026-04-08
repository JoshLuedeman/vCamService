using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFMediaBuffer (GUID 045FA593-8799-42B8-BC8D-8968C6453507).
/// Wraps a block of memory containing media data.
/// </summary>
[ComImport]
[Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaBuffer
{
    [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
    [PreserveSig] int Unlock();
    [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
    [PreserveSig] int SetCurrentLength(int cbCurrentLength);
    [PreserveSig] int GetMaxLength(out int pcbMaxLength);
}
