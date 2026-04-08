using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFMediaTypeHandler (GUID E93DCF6C-4B07-4E1E-8123-AA16ED6EADF5).
/// Retrieved from IMFStreamDescriptor to query or change the current media type.
/// </summary>
[ComImport]
[Guid("E93DCF6C-4B07-4E1E-8123-AA16ED6EADF5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaTypeHandler
{
    [PreserveSig] int IsMediaTypeSupported([MarshalAs(UnmanagedType.Interface)] IMFMediaType pMediaType, [MarshalAs(UnmanagedType.Interface)] out IMFMediaType ppMediaType);
    [PreserveSig] int GetMediaTypeCount(out int pdwTypeCount);
    [PreserveSig] int GetMediaTypeByIndex(int dwIndex, [MarshalAs(UnmanagedType.Interface)] out IMFMediaType ppType);
    [PreserveSig] int SetCurrentMediaType([MarshalAs(UnmanagedType.Interface)] IMFMediaType pMediaType);
    [PreserveSig] int GetCurrentMediaType([MarshalAs(UnmanagedType.Interface)] out IMFMediaType ppMediaType);
    [PreserveSig] int GetMajorType(out Guid pguidMajorType);
}
