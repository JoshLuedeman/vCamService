using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFSample (GUID C40A00F2-B93A-4D80-AE8C-5A1C634F58E4).
/// Inherits all IMFAttributes vtable slots, then adds sample-specific methods.
/// </summary>
[ComImport]
[Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFSample : IMFAttributes
{
    [PreserveSig] int GetSampleFlags(out int pdwSampleFlags);
    [PreserveSig] int SetSampleFlags(int dwSampleFlags);
    [PreserveSig] int GetSampleTime(out long phnsSampleTime);
    [PreserveSig] int SetSampleTime(long hnsSampleTime);
    [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
    [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
    [PreserveSig] int GetBufferCount(out int pdwBufferCount);
    [PreserveSig] int GetBufferByIndex(int dwIndex, [MarshalAs(UnmanagedType.Interface)] out IMFMediaBuffer ppBuffer);
    [PreserveSig] int ConvertToContiguousBuffer([MarshalAs(UnmanagedType.Interface)] out IMFMediaBuffer ppBuffer);
    [PreserveSig] int AddBuffer([MarshalAs(UnmanagedType.Interface)] IMFMediaBuffer pBuffer);
    [PreserveSig] int RemoveBufferByIndex(int dwIndex);
    [PreserveSig] int RemoveAllBuffers();
    [PreserveSig] int GetTotalLength(out int pcbTotalLength);
    [PreserveSig] int CopyToBuffer([MarshalAs(UnmanagedType.Interface)] IMFMediaBuffer pBuffer);
}
