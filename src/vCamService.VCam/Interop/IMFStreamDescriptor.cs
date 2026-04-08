using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// Managed COM interface for IMFStreamDescriptor (GUID 56C03D9C-9DBB-45F5-AB4B-D80F47C05938).
/// COM import — only used to receive native MF objects from P/Invoke.
/// </summary>
[ComImport]
[Guid("56C03D9C-9DBB-45F5-AB4B-D80F47C05938")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFStreamDescriptor : IMFAttributes
{
    [PreserveSig] int GetStreamIdentifier(out int pdwStreamIdentifier);
    [PreserveSig] int GetMediaTypeHandler([MarshalAs(UnmanagedType.Interface)] out IMFMediaTypeHandler ppMediaTypeHandler);
}
