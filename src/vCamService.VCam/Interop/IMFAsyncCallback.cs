using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFAsyncCallback (GUID A27003CF-2354-4F2A-8D6A-AB7CFF15437E).
/// Callback interface used with BeginGetEvent / async MF patterns.
/// </summary>
[ComImport]
[Guid("A27003CF-2354-4F2A-8D6A-AB7CFF15437E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFAsyncCallback
{
    [PreserveSig] int GetParameters(out int pdwFlags, out int pdwQueue);
    [PreserveSig] int Invoke([MarshalAs(UnmanagedType.Interface)] IMFAsyncResult pAsyncResult);
}
