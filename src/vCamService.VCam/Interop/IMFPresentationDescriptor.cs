using System.Runtime.InteropServices;

namespace vCamService.VCam.Interop;

/// <summary>
/// COM import for IMFPresentationDescriptor (GUID 03CB2711-24D7-4DB6-A17F-F3A7A479A536).
/// Describes the streams in a media source.
/// </summary>
[ComImport]
[Guid("03CB2711-24D7-4DB6-A17F-F3A7A479A536")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFPresentationDescriptor : IMFAttributes
{
    [PreserveSig] int GetStreamDescriptorCount(out int pdwDescriptorCount);
    [PreserveSig] int GetStreamDescriptorByIndex(int dwIndex, [MarshalAs(UnmanagedType.Bool)] out bool pfSelected, [MarshalAs(UnmanagedType.Interface)] out IMFStreamDescriptor ppDescriptor);
    [PreserveSig] int SelectStream(int dwDescriptorIndex);
    [PreserveSig] int DeselectStream(int dwDescriptorIndex);
    [PreserveSig] int Clone([MarshalAs(UnmanagedType.Interface)] out IMFPresentationDescriptor ppPresentationDescriptor);
}
